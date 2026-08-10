using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;

namespace PureHDF.VFD.AmazonS3;

/// <summary>
/// A stream that reads data from Amazon S3.
/// </summary>
public class AmazonS3Stream : Stream, IDatasetStream, IDisposable
{
    private readonly ConcurrentDictionary<long, IMemoryOwner<byte>> _cache = new();
    private readonly int _cacheSlotSize;
    private readonly string _bucketName;
    private readonly string _key;
    private readonly AmazonS3Client _client;

    // CONCURRENCY MODEL (async-first): one stream instance per logical reader. The cursor was a
    // ThreadLocal<long>; async continuations can resume on another thread, where that reads back as
    // 0 and every subsequent range request is issued against the wrong offset.
    private long _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmazonS3Stream" /> instance.
    /// </summary>
    /// <param name="client">The Amazon S3 client.</param>
    /// <param name="bucketName">The bucket name.</param>
    /// <param name="key">The key that identifies the object in the bucket.</param>
    /// <param name="cacheSlotSize">The size of a single cache slot.</param>
    public AmazonS3Stream(AmazonS3Client client, string bucketName, string key, int cacheSlotSize = 1 * 1024 * 1024)
    {
        if (cacheSlotSize <= 0)
            throw new Exception("Cache slot size must be > 0");

        _client = client;
        _bucketName = bucketName;
        _key = key;
        _cacheSlotSize = cacheSlotSize;

        // https://registry.opendata.aws/nrel-pds-wtk/
        Length = client
            .GetObjectMetadataAsync(bucketName, key)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult()
            .ContentLength;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length { get; }

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadCached(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    /// <inheritdoc />
    // NOTE (async-first): dataset reads are now genuinely async — no GetAwaiter().GetResult().
    // The synchronous Stream.Read override below still blocks, because System.IO.Stream's own
    // contract is synchronous; only this IDatasetStream path is await-able end to end.
    public async ValueTask ReadDataset(Memory<byte> buffer)
    {
        await ReadUncachedAsync(buffer).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:

                _position = offset;

                if (!(0 <= _position && _position < Length))
                    throw new Exception("The offset exceeds the stream length.");

                return _position;

            case SeekOrigin.Current:

                _position += offset;

                if (!(0 <= _position && _position < Length))
                    throw new Exception("The offset exceeds the stream length.");

                return _position;
        }

        throw new Exception($"Seek origin '{origin}' is not supported.");
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotImplementedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotImplementedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var entry in _cache)
            {
                entry.Value.Dispose();
            }
        }
    }

    private async ValueTask<int> ReadUncachedAsync(Memory<byte> buffer)
    {
        var stream = await ReadDataFromS3Async(
            start: Position,
            end: Position + buffer.Length).ConfigureAwait(false);

        await ReadExactlyAsync(stream, buffer).ConfigureAwait(false);

        return buffer.Length;
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer)
    {
        var remaining = buffer;

        while (remaining.Length > 0)
        {
            var read = await stream.ReadAsync(remaining).ConfigureAwait(false);

            if (read == 0)
                throw new EndOfStreamException();

            remaining = remaining[read..];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadCached(Span<byte> buffer)
    {
        // TODO issue parallel requests
        var s3UpperLength = Math.Max(_cacheSlotSize, buffer.Length);
        var s3Remaining = Length - _position;
        var s3ActualLength = (int)Math.Min(s3UpperLength, s3Remaining);
        var s3Processed = 0;
        var s3StartIndex = -1L;
        var remainingBuffer = buffer;

        bool loadFromS3;

        while (s3Processed < s3ActualLength)
        {
            var currentIndex = (_position + s3Processed) / _cacheSlotSize;
            loadFromS3 = false;

            // determine if data is cached
            var owner = _cache.GetOrAdd(currentIndex, currentIndex =>
            {
                var owner = MemoryPool<byte>.Shared.Rent(_cacheSlotSize);

                // first index for which data will be requested
                if (s3StartIndex == -1)
                    s3StartIndex = currentIndex;

                loadFromS3 = true;

                return owner;
            });

            if (!loadFromS3 /* i.e. data is in cache */)
            {
                // is there a not yet loaded range of data?
                if (s3StartIndex != -1)
                {
                    var s3EndIndex = currentIndex + 1;
                    remainingBuffer = LoadFromS3ToCacheAndBuffer(s3StartIndex, s3EndIndex, remainingBuffer);
                    s3StartIndex = -1;
                }

                // copy from cache
                remainingBuffer = CopyFromCacheToBuffer(currentIndex, owner, remainingBuffer);
            }

            s3Processed += _cacheSlotSize;
        }

        // TODO code duplication
        // is there a not yet loaded range of data?
        if (s3StartIndex != -1)
        {
            var s3EndIndex = s3StartIndex + s3ActualLength / _cacheSlotSize;
            remainingBuffer = LoadFromS3ToCacheAndBuffer(s3StartIndex, s3EndIndex, remainingBuffer);
            s3StartIndex = -1;
        }

        return buffer.Length;
    }

    private Span<byte> LoadFromS3ToCacheAndBuffer(
        long s3StartIndex,
        long s3EndIndex,
        Span<byte> remainingBuffer)
    {
        // get S3 stream
        var s3Start = s3StartIndex * _cacheSlotSize;
        var s3End = Math.Min(s3EndIndex * _cacheSlotSize, Length);

        var stream = ReadDataFromS3(
            start: s3Start,
            end: s3End);

        // copy
        for (long currentIndex = s3StartIndex; currentIndex < s3EndIndex; currentIndex++)
        {
            var owner = _cache.GetOrAdd(currentIndex, _ => throw new Exception("This should never happen."));

            // copy to cache
            var buffer = owner.Memory[..(int)Math.Min(_cacheSlotSize, Length - Position)];
            ReadExactly(stream, buffer.Span);

            // copy to request buffer
            remainingBuffer = CopyFromCacheToBuffer(currentIndex, owner, remainingBuffer);
        }

        return remainingBuffer;
    }

    private Span<byte> CopyFromCacheToBuffer(long currentIndex, IMemoryOwner<byte> owner, Span<byte> remainingBuffer)
    {
        var s3Position = currentIndex * _cacheSlotSize;

        var cacheSlotOffset = _position > s3Position
            ? (int)(_position - s3Position)
            : 0;

        var remainingCacheSlotSize = _cacheSlotSize - cacheSlotOffset;

        var slicedMemory = owner.Memory
            .Slice(cacheSlotOffset, Math.Min(remainingCacheSlotSize, remainingBuffer.Length));

        slicedMemory.Span.CopyTo(remainingBuffer);

        remainingBuffer = remainingBuffer[slicedMemory.Length..];
        _position += slicedMemory.Length;

        return remainingBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Stream ReadDataFromS3(long start, long end)
    {
        return ReadDataFromS3Async(start, end)
            .GetAwaiter()
            .GetResult();
    }

    private async ValueTask<Stream> ReadDataFromS3Async(long start, long end)
    {
        var request = new GetObjectRequest()
        {
            BucketName = _bucketName,
            Key = _key,
            ByteRange = new ByteRange(start, end)
        };

        var response = await _client.GetObjectAsync(request).ConfigureAwait(false);

        return response.ResponseStream;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var slicedBuffer = buffer;

        while (slicedBuffer.Length > 0)
        {
            var readBytes = stream.Read(slicedBuffer);
            slicedBuffer = slicedBuffer[readBytes..];
        };
    }
}
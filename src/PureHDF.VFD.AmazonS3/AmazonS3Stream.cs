using System.Buffers;
using System.Collections.Concurrent;
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

    // THREAD SAFETY: IDatasetStream requires both of its methods to be safe to call concurrently, and
    // PureHDF does call them concurrently - a positionless stream gets one driver per read operation.
    //
    // ReadDataset needs no protection: it issues a range request for exactly the caller's buffer and
    // touches no shared state.
    //
    // The cache behind ReadMetadata does. It is not enough that _cache is a ConcurrentDictionary: a
    // slot is inserted EMPTY and filled afterwards, so a second thread finding it present would copy
    // uninitialized memory, and GetOrAdd's factory has side effects (it flags "load this range" and
    // rents a buffer) which are silently discarded when another thread wins the insert - leaking the
    // rented buffer and mis-flagging the range. Both are data races producing wrong bytes, not just
    // wasted work, so the whole cached path runs under a mutex.
    //
    // A SemaphoreSlim rather than a lock, because the path awaits its range requests. The mutex is
    // held across those awaits: metadata reads are small and, after warm-up, served from the cache
    // without any request at all, so serializing them costs little - and the alternative (a per-slot
    // async gate) would mean giving up the batching of adjacent missing slots into one request.
    private readonly SemaphoreSlim _cacheLock = new(initialCount: 1, maxCount: 1);

    // CONCURRENCY MODEL: this cursor belongs to the base Stream contract (Position / Seek / Read)
    // only. PureHDF does not use it - it reads through IDatasetStream, which carries an absolute
    // offset per call - so concurrent PureHDF reads never touch it. A caller mixing the synchronous
    // Stream API with concurrent reads from several threads is on their own, exactly as for any
    // other Stream.
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
    // NOTE: System.IO.Stream's own contract is synchronous and cursor-based, so this override still
    // blocks and still moves _position. It is a separate contract from IDatasetStream below, kept
    // working unchanged; PureHDF itself never comes through here.
    public override int Read(byte[] buffer, int offset, int count)
    {
        var slice = buffer.AsMemory(offset, count);

        ReadCachedAsync(_position, slice)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        _position += count;

        return count;
    }

    /// <inheritdoc />
    // Bulk payload: requested as its own byte range and never cached. A dataset chunk is typically
    // large and decoded once, so caching it would only displace the metadata that is re-read
    // constantly.
    public ValueTask ReadDataset(long offset, Memory<byte> buffer)
    {
        return ReadUncachedAsync(offset, buffer);
    }

    /// <inheritdoc />
    // Structure: small, numerous and highly repetitive reads, served from fixed-size cache slots so
    // that one range request covers many of them.
    public ValueTask ReadMetadata(long offset, Memory<byte> buffer)
    {
        return ReadCachedAsync(offset, buffer);
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

            _cacheLock.Dispose();
        }
    }

    private async ValueTask ReadUncachedAsync(long offset, Memory<byte> buffer)
    {
        var stream = await ReadDataFromS3Async(
            start: offset,
            end: offset + buffer.Length).ConfigureAwait(false);

        await ReadExactlyAsync(stream, buffer).ConfigureAwait(false);
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

    private async ValueTask ReadCachedAsync(long offset, Memory<byte> buffer)
    {
        // See the _cacheLock note above: everything below mutates the cache, and a half-filled slot
        // published to another thread would be read as data.
        await _cacheLock.WaitAsync().ConfigureAwait(false);

        try
        {
            // The cursor is a local, threaded through the helpers: this path is positionless and must
            // not observe or move _position.
            var position = offset;

            // TODO issue parallel requests
            var s3UpperLength = Math.Max(_cacheSlotSize, buffer.Length);
            var s3Remaining = Length - position;
            var s3ActualLength = (int)Math.Min(s3UpperLength, s3Remaining);
            var s3Processed = 0;
            var s3StartIndex = -1L;
            var remainingBuffer = buffer;

            bool loadFromS3;

            while (s3Processed < s3ActualLength)
            {
                var currentIndex = (position + s3Processed) / _cacheSlotSize;
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

                        (position, remainingBuffer) = await LoadFromS3ToCacheAndBufferAsync(
                            s3StartIndex, s3EndIndex, position, remainingBuffer).ConfigureAwait(false);

                        s3StartIndex = -1;
                    }

                    // copy from cache
                    (position, remainingBuffer) = CopyFromCacheToBuffer(currentIndex, owner, position, remainingBuffer);
                }

                s3Processed += _cacheSlotSize;
            }

            // TODO code duplication
            // is there a not yet loaded range of data?
            if (s3StartIndex != -1)
            {
                var s3EndIndex = s3StartIndex + s3ActualLength / _cacheSlotSize;

                (position, remainingBuffer) = await LoadFromS3ToCacheAndBufferAsync(
                    s3StartIndex, s3EndIndex, position, remainingBuffer).ConfigureAwait(false);
            }
        }

        finally
        {
            _cacheLock.Release();
        }
    }

    private async ValueTask<(long Position, Memory<byte> RemainingBuffer)> LoadFromS3ToCacheAndBufferAsync(
        long s3StartIndex,
        long s3EndIndex,
        long position,
        Memory<byte> remainingBuffer)
    {
        // get S3 stream
        var s3Start = s3StartIndex * _cacheSlotSize;
        var s3End = Math.Min(s3EndIndex * _cacheSlotSize, Length);

        var stream = await ReadDataFromS3Async(
            start: s3Start,
            end: s3End).ConfigureAwait(false);

        // copy
        for (long currentIndex = s3StartIndex; currentIndex < s3EndIndex; currentIndex++)
        {
            var owner = _cache.GetOrAdd(currentIndex, _ => throw new Exception("This should never happen."));

            // copy to cache
            var buffer = owner.Memory[..(int)Math.Min(_cacheSlotSize, Length - position)];
            await ReadExactlyAsync(stream, buffer).ConfigureAwait(false);

            // copy to request buffer
            (position, remainingBuffer) = CopyFromCacheToBuffer(currentIndex, owner, position, remainingBuffer);
        }

        return (position, remainingBuffer);
    }

    private (long Position, Memory<byte> RemainingBuffer) CopyFromCacheToBuffer(
        long currentIndex,
        IMemoryOwner<byte> owner,
        long position,
        Memory<byte> remainingBuffer)
    {
        var s3Position = currentIndex * _cacheSlotSize;

        var cacheSlotOffset = position > s3Position
            ? (int)(position - s3Position)
            : 0;

        var remainingCacheSlotSize = _cacheSlotSize - cacheSlotOffset;

        var slicedMemory = owner.Memory
            .Slice(cacheSlotOffset, Math.Min(remainingCacheSlotSize, remainingBuffer.Length));

        slicedMemory.CopyTo(remainingBuffer);

        return (position + slicedMemory.Length, remainingBuffer[slicedMemory.Length..]);
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
}

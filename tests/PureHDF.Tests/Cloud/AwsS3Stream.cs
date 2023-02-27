using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;

namespace PureHDF.Tests
{
    public class AwsS3Stream : Stream, IDisposable
    {
        private readonly ConcurrentDictionary<long, IMemoryOwner<byte>> _cache = new();
        private readonly int _cacheSlotSize;
        private long _position;
        private readonly string _bucketName;
        private readonly string _key;
        private readonly AmazonS3Client _client;

        public AwsS3Stream(AmazonS3Client client, string bucketName, string key, int cacheSlotSize = 1 * 1024 * 1024)
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

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position 
        { 
            get => _position; 
            set => _position = value; 
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public override int Read(Span<byte> buffer)
        {
            return ReadCore(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // TODO revert
            return ReadCore(buffer.AsSpan().Slice(offset, count));
            // throw new NotImplementedException();
        }
#else
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadCore(buffer.AsSpan(offset, count));
        }
#endif

        // TODO revert
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadCore(Span<byte> buffer)
        {
            // TODO issue parallel requests
            // TODO do not cache dataset data
            var remainingBuffer = buffer;

            while (remainingBuffer.Length > 0)
            {
                var cacheSlotIndex = _position / _cacheSlotSize;
                var actualPosition = cacheSlotIndex * _cacheSlotSize;
                var actualLength = (int)Math.Min(Math.Max(_cacheSlotSize, remainingBuffer.Length), Length - _position);

                // get cache entry
                var data = _cache.GetOrAdd(cacheSlotIndex, _ => 
                {
                    var owner = MemoryPool<byte>.Shared.Rent(actualLength);
                    var memory = owner.Memory[..actualLength];

                    // TODO request should be as large as requested (minimum cache slot size)
                    // And then distribute it to the cache slots
                    var request = new GetObjectRequest()
                    {
                        BucketName = _bucketName,
                        Key = _key,
                        ByteRange = new ByteRange(actualPosition, actualPosition + actualLength)
                    };

                    var response = _client
                        .GetObjectAsync(request)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    var stream = response.ResponseStream;

#if NET7_0_OR_GREATER
                    stream.ReadExactly(memory.Span);
#else
                    var slicedBuffer = memory.Span;

                    while (slicedBuffer.Length > 0)
                    {
                        var readBytes = stream.Read(slicedBuffer);
                        slicedBuffer = slicedBuffer[readBytes..];
                    };
#endif
                    return owner;
                });

                // copy data
                var bufferLength = Math.Min(actualLength, remainingBuffer.Length);
                
                data
                    .Memory.Span
                    .Slice((int)(_position - actualPosition), bufferLength)
                    .CopyTo(remainingBuffer);

                remainingBuffer = remainingBuffer[Math.Min(actualLength, bufferLength)..];
            }

            _position += buffer.Length;

            return buffer.Length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(buffer, cancellationToken);
        }

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

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Flush() => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var (_, cacheEntry) in _cache)
                {
                    cacheEntry.Dispose();
                }
            }
        }
    }
}

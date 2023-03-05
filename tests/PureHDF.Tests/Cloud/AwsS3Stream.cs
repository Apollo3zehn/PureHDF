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
            var s3UpperLength = Math.Max(_cacheSlotSize, buffer.Length);
            var s3Remaining = Length - _position;
            var s3ActualLength = (int)Math.Min(s3UpperLength, s3Remaining);
            var s3Processed = 0;
            var s3StartIndex = -1L;
            var remainingBuffer = buffer;

            bool loadFromS3;

            while (s3Processed < s3ActualLength)
            {
                var currentIndex = _position / _cacheSlotSize;
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
                        LoadFromS3ToCacheAndBuffer(s3StartIndex, s3EndIndex: currentIndex, ref remainingBuffer);
                        s3StartIndex = -1;
                    }

                    // copy from cache
                    CopyFromCacheToBuffer(currentIndex, owner, ref remainingBuffer);
                }

                s3Processed += _cacheSlotSize;
            }

            // TODO code duplication
            // is there a not yet loaded range of data?
            if (s3StartIndex != -1)
            {
                var s3EndIndex = s3ActualLength / _cacheSlotSize;
                LoadFromS3ToCacheAndBuffer(s3StartIndex, s3EndIndex, ref remainingBuffer);
                s3StartIndex = -1;
            }

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
    
        private void LoadFromS3ToCacheAndBuffer(long s3StartIndex, long s3EndIndex, ref Span<byte> remainingBuffer)
        {
            // get S3 stream
            var s3Start = s3StartIndex * _cacheSlotSize;
            var s3Length = (s3EndIndex - s3StartIndex) * _cacheSlotSize;
            var s3Remaining = Length - s3Start;
            var s3ActualLength = Math.Min(s3Length, s3Remaining);

            var request = new GetObjectRequest()
            {
                BucketName = _bucketName,
                Key = _key,
                ByteRange = new ByteRange(s3Start, s3ActualLength)
            };

            var response = _client
                .GetObjectAsync(request)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var stream = response.ResponseStream;

            // copy
            for (long currentIndex = s3StartIndex; currentIndex < s3EndIndex; currentIndex++)
            {
                var owner = _cache.GetOrAdd(currentIndex, _ => throw new Exception("This should never happen."));

                // copy to cache
                var memory = owner.Memory.Slice(0, Math.Min(_cacheSlotSize, (int)(Length - Position)));

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

                // copy to request buffer
                CopyFromCacheToBuffer(currentIndex, owner, ref remainingBuffer);
            }
        }

        private void CopyFromCacheToBuffer(long currentIndex, IMemoryOwner<byte> owner, ref Span<byte> remainingBuffer)
        {
            var s3Position = currentIndex * _cacheSlotSize;
            var cacheSlotOffset = (int)(_position - s3Position);
            var remainingCacheSlotSize = _cacheSlotSize - cacheSlotOffset;

            var slicedMemory = owner.Memory
                .Slice(cacheSlotOffset, Math.Min(remainingCacheSlotSize, remainingBuffer.Length));

            slicedMemory.Span.CopyTo(remainingBuffer);

            remainingBuffer = remainingBuffer[slicedMemory.Length..];
            _position += slicedMemory.Length;
        }
    }
}

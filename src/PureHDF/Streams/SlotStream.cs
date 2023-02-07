using System.Runtime.CompilerServices;

namespace PureHDF
{
    internal class SlotStream : Stream
    {
        private long _position;
        private readonly H5File _file;
        private readonly LocalHeap _heap;
        private Stream? _stream;
        private readonly ExternalFileListSlot _slot;
        private readonly H5DatasetAccess _datasetAccess;

        public SlotStream(H5File file, LocalHeap heap, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
        {
            _file = file;
            _heap = heap;
            _slot = slot;
            Offset = offset;
            _datasetAccess = datasetAccess;
        }

        public long Offset { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => (long)_slot.Size;

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public override int Read(Span<byte> buffer)
        {
            return ReadCore(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
#else
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadCore(buffer.AsSpan(offset, count));
        }
#endif

        public int ReadCore(Span<byte> buffer)
        {
            var length = (int)Math.Min(Length - Position, buffer.Length);

            _stream = EnsureStream();

            var actualLength = _stream.Read(buffer[..length]);

            // If file is shorter than slot: fill remaining buffer with zeros.
            buffer[actualLength..length]
                .Clear();

            _position += length;

            return length;
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            return ReadAsyncCore(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
#else
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async ValueTask<int> ReadAsyncCore(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var length = (int)Math.Min(Length - Position, buffer.Length);

            _stream = EnsureStream();

            var actualLength = await _stream
                .ReadAsync(buffer[..length], cancellationToken)
                .ConfigureAwait(false);

            // If file is shorter than slot: fill remaining buffer with zeros.
            buffer
                .Span[actualLength..length]
                .Fill(0);

            _position += length;

            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:

                    if (offset > Length)
                        throw new Exception("The offset exceeds the stream length.");

                    _stream = EnsureStream();
                    _stream.Seek(offset + (long)_slot.Offset, origin);
                    _position = offset;

                    return _position;
            }

            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            _stream?.Dispose();

            base.Dispose(disposing);
        }

        // TODO File should be opened asynchronously if root file is also opened asynchronously. Then implement ReadAsync here.
        private Stream EnsureStream()
        {
            if (_stream is null)
            {
                var name = _heap.GetObjectName(_slot.NameHeapOffset);
                var filePath = FilePathUtils.FindExternalFileForDatasetAccess(_file.FolderPath, name, _datasetAccess);

                if (!File.Exists(filePath))
                    throw new Exception($"External file '{filePath}' does not exist.");

                _stream = File.OpenRead(filePath!);
                _stream.Seek((long)_slot.Offset, SeekOrigin.Begin);
            }

            return _stream;
        }
    }
}

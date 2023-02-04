using System;

namespace PureHDF
{
    internal class SlotStream : Stream
    {
        private long _position;
        private readonly LocalHeap _heap;
        private Stream? _stream;
        private readonly ExternalFileListSlot _slot;
        private readonly H5DatasetAccess _datasetAccess;

        public SlotStream(LocalHeap heap, ExternalFileListSlot slot, long offset, H5DatasetAccess datasetAccess)
        {
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = (int)Math.Min(Length - Position, count);

            _stream = EnsureStream();

            var actualLength = _stream.Read(buffer, offset, length);

            // If file is shorter than slot: fill remaining buffer with zeros.
            buffer
                .AsSpan()
                .Slice(offset + actualLength, length - actualLength)
                .Fill(0);

            _position += length;

            return length;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var length = (int)Math.Min(Length - Position, count);

            _stream = EnsureStream();

            var actualLength = await _stream
                .ReadAsync(buffer.AsMemory(offset, length), cancellationToken)
                .ConfigureAwait(false);

            // If file is shorter than slot: fill remaining buffer with zeros.
            buffer
                .AsSpan()
                .Slice(offset + actualLength, length - actualLength)
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
                var filePath = FilePathUtils.FindExternalFileForDatasetAccess(name, _datasetAccess);

                if (!File.Exists(filePath))
                    throw new Exception($"External file '{filePath}' does not exist.");

                _stream = File.OpenRead(filePath);
                _stream.Seek((long)_slot.Offset, SeekOrigin.Begin);
            }

            return _stream;
        }
    }
}

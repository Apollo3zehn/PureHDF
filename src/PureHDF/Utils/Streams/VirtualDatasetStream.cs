namespace PureHDF
{
    internal class VirtualDatasetStream : Stream
    {
        private readonly int _typeSize;
        private readonly VdsDatasetEntry[] _entries;
        private long _position;

        public VirtualDatasetStream(VdsDatasetEntry[] entries, int typeSize)
        {
            _entries = entries;
            _typeSize = typeSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get => throw new NotImplementedException(); }

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

        public override int Read(Span<byte> buffer)
        {
            // TODO: now find out which VdsDataSetEntry holds these data, if none: fill value.
            // - There are two kinds to hyperslab selections (and other selection types, too).
            // - For hyper selection type 1: use binary search to quickly find block.

            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        // public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        // {
            
        // }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                _position = offset;
                return offset;
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
            base.Dispose(disposing);
        }
    }
}

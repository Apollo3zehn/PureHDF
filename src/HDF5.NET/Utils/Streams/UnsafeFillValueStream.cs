namespace HDF5.NET
{
    internal class UnsafeFillValueStream : Stream
    {
        private readonly byte[] _fillValue;
        private readonly int _length;
        private long _position;

        public UnsafeFillValueStream(byte[] fillValue)
        {
            _fillValue = fillValue.ToArray();
            _length = _fillValue.Length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => long.MaxValue;

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

        // ReadAsync: https://devblogs.microsoft.com/pfxteam/overriding-stream-asynchrony/
        // see "If you don’t override ..."
        public override unsafe int Read(byte[] buffer, int offset, int count)
        {
            unsafe
            {
                fixed (byte* ptrSrc = _fillValue, ptrDst = buffer)
                {
                    for (int i = 0; i < count; i++)
                    {
                        ptrDst[offset + i] = ptrSrc[(_position + i) % _length];
                    }
                }
            }

            _position += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position += offset; return _position;
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
    }
}

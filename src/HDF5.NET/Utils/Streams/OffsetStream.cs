namespace HDF5.NET
{
    internal class OffsetStream : Stream
    {
        private readonly H5BaseReader _reader;
        private readonly long _baseAddress;

        public OffsetStream(H5BaseReader reader)
        {
            _reader = reader;
            _baseAddress = reader.Position;
        }

        public override bool CanRead => _reader.CanRead;

        public override bool CanSeek => _reader.CanSeek;

        public override bool CanWrite => _reader.CanWrite;

        public override long Length => _reader.Length - _baseAddress;

        public override long Position
        {
            get => _reader.Position - _baseAddress;
            set => _reader.Position = _baseAddress + value;
        }

        public override void Flush()
        {
            _reader.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _reader.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return origin switch
            {
                SeekOrigin.Begin => _reader.Seek(_baseAddress + offset, SeekOrigin.Begin),
                SeekOrigin.Current => _reader.Seek(offset, SeekOrigin.Current),
                _ => throw new Exception($"Seek origin '{origin}' is not supported.")
            };
        }

        public override void SetLength(long value)
        {
            _reader.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _reader.Write(buffer, offset, count);
        }
    }
}

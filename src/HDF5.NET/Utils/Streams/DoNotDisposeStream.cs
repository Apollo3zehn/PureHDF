namespace HDF5.NET
{
    internal class DoNotDisposeStream : Stream
    {
        private H5BaseReader _reader;

        public DoNotDisposeStream(H5BaseReader reader)
        {
            _reader = reader;
        }

        public override bool CanRead => _reader.CanRead;

        public override bool CanSeek => _reader.CanSeek;

        public override bool CanWrite => _reader.CanWrite;

        public override long Length => _reader.Length;

        public override long Position 
        { 
            get => _reader.Position; 
            set => _reader.Position = value; 
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
            return _reader.Seek(offset, origin);
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

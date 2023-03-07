namespace PureHDF
{
    internal class MemorySpanStream : Stream
    {
        private long _position;
        private readonly Memory<byte> _memory;
        private Memory<byte> _sliced;

        public MemorySpanStream(Memory<byte> memory)
        {
            _memory = memory;
            _sliced = memory;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _memory.Length;

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(_sliced.Length, count);

            _sliced[..length]
                .CopyTo(buffer.AsMemory()[offset..]);

            Position += length;

            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:

                    if (offset > _memory.Length)
                        throw new NotSupportedException("Cannot seek behind the end of the array.");

                    _sliced = _memory[(int)offset..];
                    _position = offset;

                    break;

                case SeekOrigin.Current:

                    if (offset > _sliced.Length)
                        throw new NotSupportedException("Cannot seek behind the end of the array.");

                    _sliced = _sliced[(int)offset..];
                    _position += offset;

                    break;

                case SeekOrigin.End:

                    if (offset > _sliced.Length)
                        throw new NotSupportedException("Cannot seek before the start of the array.");

                    _sliced = _memory[^((int)offset)..];
                    _position = _memory.Length - (int)offset;

                    break;

                default:
                    throw new NotSupportedException("Unknown seek origin.");
            }

            return Position;
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
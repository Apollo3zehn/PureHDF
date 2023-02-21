namespace PureHDF
{
    internal class MemorySpanStream : Stream
    {
        private long _position;

        public MemorySpanStream(Memory<byte> memory)
        {
            OriginalMemory = memory;
            SlicedMemory = memory;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => OriginalMemory.Length;

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

        public Memory<byte> OriginalMemory { get; }
        
        public Memory<byte> SlicedMemory { get; private set; }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(SlicedMemory.Length, count);

            SlicedMemory[..length]
                .CopyTo(buffer.AsMemory()[offset..]);

            Position += length;

            return length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:

                    if (offset > OriginalMemory.Length)
                        throw new NotSupportedException("Cannot seek behind the end of the array.");

                    SlicedMemory = OriginalMemory[(int)offset..];
                    _position = offset;

                    break;

                case SeekOrigin.Current:

                    if (offset > SlicedMemory.Length)
                        throw new NotSupportedException("Cannot seek behind the end of the array.");

                    SlicedMemory = SlicedMemory[(int)offset..];
                    _position += offset;

                    break;

                case SeekOrigin.End:

                    if (offset > SlicedMemory.Length)
                        throw new NotSupportedException("Cannot seek before the start of the array.");

                    SlicedMemory = OriginalMemory[^((int)offset)..];
                    _position = OriginalMemory.Length - (int)offset;

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

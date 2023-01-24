using System.IO.MemoryMappedFiles;

namespace HDF5.NET
{
    internal class H5MemoryMappedFileReader : H5BaseReader
    {
        private readonly ThreadLocal<long> _position = new();
        private MemoryMappedViewAccessor _accessor;

        public H5MemoryMappedFileReader(MemoryMappedViewAccessor accessor) : base(accessor.Capacity)
        {
            _accessor = accessor;
        }

        public override long Position
        {
            get => _position.Value;
            set => throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin seekOrigin)
        {
            switch (seekOrigin)
            {
                case SeekOrigin.Begin:
                    _position.Value = (long)BaseAddress + offset; break;

                case SeekOrigin.Current:
                    _position.Value += offset; break;

                default:
                    throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
            }

            return offset;
        }

        public override int Read(Span<byte> buffer)
        {
            // https://github.com/dotnet/runtime/issues/42736

            unsafe {
                byte* ptr = null;

                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                
                try
                {
                    var length = _accessor.SafeMemoryMappedViewHandle.ByteLength;
                    ... // use ptr and length here
                }
                finally
                {
                    _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }

            _position.Value += buffer.Length;

            return buffer.Length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            throw new Exception($"Memory-mapped files cannot be access asynchronously.");
        }

        public override byte ReadByte()
        {
            return _accessor.ReadByte(_position.Value);
        }

        public override byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            Read(buffer);
            
            return buffer;
        }

        public override ushort ReadUInt16()
        {
            return _accessor.ReadUInt16(_position.Value);
        }

        public override short ReadInt16()
        {
            return _accessor.ReadInt16(_position.Value);
        }

        public override uint ReadUInt32()
        {
            return _accessor.ReadUInt32(_position.Value);
        }

        public override ulong ReadUInt64()
        {
            return _accessor.ReadUInt64(_position.Value);
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_disposedValue)
            {
                if (disposing)
                {
                    _accessor.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}
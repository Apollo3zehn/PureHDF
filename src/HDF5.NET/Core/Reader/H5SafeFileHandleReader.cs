#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HDF5.NET
{
    internal class H5SafeFileHandleReader : H5BaseReader
    {
        private readonly ThreadLocal<long> _position = new();

        public H5SafeFileHandleReader(SafeFileHandle handle, long length) : base(length)
        {
            Handle = handle;
        }

        public SafeFileHandle Handle { get; }

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
            var count = RandomAccess.Read(Handle, buffer, Position);
            _position.Value += buffer.Length;

            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            var count = RandomAccess.ReadAsync(Handle, buffer, Position);
            _position.Value += buffer.Length;

            return count;
        }

        public override byte ReadByte()
        {
            return Read<byte>();
        }

        public override byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            Read(buffer);
            
            return buffer;
        }

        public override ushort ReadUInt16()
        {
            return Read<ushort>();
        }

        public override short ReadInt16()
        {
            return Read<short>();
        }

        public override uint ReadUInt32()
        {
            return Read<uint>();
        }

        public override ulong ReadUInt64()
        {
            return Read<ulong>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T Read<T>() where T : unmanaged
        {
            var size = Unsafe.SizeOf<T>();
            Span<byte> buffer = stackalloc byte[size];
            RandomAccess.Read(Handle, buffer, Position);
            _position.Value += size;
            
            return MemoryMarshal.Cast<byte, T>(buffer)[0];
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_disposedValue)
            {
                if (disposing)
                {
                    Handle.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}

#endif
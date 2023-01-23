using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    internal class H5StreamReader : H5BinaryReader
    {
        private readonly Stream _stream;

        public H5StreamReader(Stream stream)
        {
            _stream = stream;
        }

        public override long Position => _stream.Position - (long)BaseAddress;
        public override long Length => _stream.Length;

        public override void Seek(long offset, SeekOrigin seekOrigin)
        {
            switch (seekOrigin)
            {
                case SeekOrigin.Begin:
                    _stream.Seek((long)BaseAddress + offset, SeekOrigin.Begin); break;

                case SeekOrigin.Current:
                    _stream.Seek(offset, SeekOrigin.Current); break;

                default:
                    throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
            }
        }

        public override byte ReadByte()
        {
            return (byte)_stream.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];
            _stream.Read(buffer);
            
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
            _stream.Read(buffer);
            
            return MemoryMarshal.Cast<byte, T>(buffer)[0];
        }

        public override void Dispose()
        {
            _stream.Dispose();
        }
    }
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    internal class H5StreamReader : H5BaseReader
    {
        private readonly bool _leaveOpen;
        private readonly Stream _stream;

        public H5StreamReader(Stream stream, bool leaveOpen) : base(stream.Length)
        {
            if (!stream.CanRead)
                throw new Exception("The stream must be readable.");

            if (!stream.CanSeek)
                throw new Exception("The stream must be seekable.");

            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        public override long Position
        {
            get => _stream.Position - (long)BaseAddress;
            set => throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin seekOrigin)
        {
            return seekOrigin switch
            {
                SeekOrigin.Begin => _stream.Seek((long)BaseAddress + offset, SeekOrigin.Begin),
                SeekOrigin.Current => _stream.Seek(offset, SeekOrigin.Current),
                _ => throw new Exception($"Seek origin '{seekOrigin}' is not supported."),
            };
        }

        public override int Read(Span<byte> buffer)
        {
            return _stream.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer)
        {
            throw new Exception($"The stream of type {_stream.GetType().FullName} is not thread-safe.");
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

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (!_leaveOpen)
                        _stream.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}

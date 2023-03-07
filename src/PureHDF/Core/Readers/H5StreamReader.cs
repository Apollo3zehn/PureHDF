using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF
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

        public override long Position { get => _stream.Position - (long)BaseAddress; }

        public override void Seek(long offset, SeekOrigin seekOrigin)
        {
            switch (seekOrigin)
            {
                case SeekOrigin.Begin:
                     _stream.Seek((long)BaseAddress + offset, SeekOrigin.Begin);
                    break;

                case SeekOrigin.Current:
                    _stream.Seek(offset, SeekOrigin.Current);
                    break;

                default:
                    throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
            };
        }

        public override void Read(Memory<byte> buffer)
        {
            var remainingBuffer = buffer;

            while (remainingBuffer.Length > 0)
            {
                var count = _stream.Read(buffer.Span);
                remainingBuffer = remainingBuffer[count..];
            }
        }

        public override async ValueTask ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var remainingBuffer = buffer;

            while (remainingBuffer.Length > 0)
            {
                var count = await _stream.ReadAsync(buffer, cancellationToken);
                remainingBuffer = remainingBuffer[count..];
            }
        }

        public override byte ReadByte()
        {
            return Read<byte>();
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

#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PureHDF
{
    internal class H5FileStreamReader : H5BaseReader
    {
        private readonly ThreadLocal<long> _position = new();
        private readonly FileStream _stream; // it is important to keep a reference, otherwise the SafeFileHandle gets closed during the next GC
        private readonly SafeFileHandle _handle;
        private readonly bool _leaveOpen;

        public H5FileStreamReader(FileStream stream, bool leaveOpen) : base(stream.Length)
        {
            _stream = stream;
            _handle = _stream.SafeFileHandle;

            IsAsync = _handle.IsAsync;

            _leaveOpen = leaveOpen;
        }

        public bool IsAsync { get; }

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
            var count = RandomAccess.Read(_handle, buffer, Position);
            _position.Value += buffer.Length;

            return count;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var count = RandomAccess.ReadAsync(_handle, buffer, Position, cancellationToken);
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
            RandomAccess.Read(_handle, buffer, Position);
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
                    if (!_leaveOpen)
                        _stream.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}

#endif
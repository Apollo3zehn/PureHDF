using System.IO.MemoryMappedFiles;

namespace PureHDF.VFD;

// AcquirePointer: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L120-L138
// Read single element: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryAccessor.cs#L148

// Does it make sense to acquire pointer only once instead of every read operation?
// https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround
// -> I think the synchrnonization complaint is not valid anymore: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L27-L28
//
// NOTE (async-first spike): a memory-mapped view is a genuinely synchronous source, so every
// member here returns an already-completed ValueTask. Sync-completing ValueTasks do not allocate,
// which is the whole point of async-first: remote sources get real async, local ones pay nothing.
// CONCURRENCY MODEL (async-first): as with H5FileHandleDriver, a driver instance belongs to one
// logical reader. The cursor was a ThreadLocal<long>, which async breaks (a continuation may resume
// on another thread, where it reads back as 0). Callers wanting parallelism open one reader each.
internal unsafe partial class H5MemoryMappedFileDriver : H5DriverBase
{
    private long _position;
    private readonly MemoryMappedViewAccessor _accessor;

    public H5MemoryMappedFileDriver(MemoryMappedViewAccessor accessor)
    {
        _accessor = accessor;
    }

    public override long Position { get => _position; }

    public override long Length => _accessor.Capacity;

    public override void Seek(long offset, SeekOrigin seekOrigin)
    {
        switch (seekOrigin)
        {
            case SeekOrigin.Begin:
                _position = offset; break;

            case SeekOrigin.Current:
                _position += offset; break;

            default:
                throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
        }
    }

    public override ValueTask ReadDataset(Memory<byte> buffer)
    {
        ReadCore(buffer.Span);

        return default;
    }

    public override ValueTask Read(Memory<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<byte> ReadByte()
    {
        var value = _accessor.ReadByte(_position);
        _position += sizeof(byte);

        return new ValueTask<byte>(value);
    }

    public override ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        ReadCore(buffer);

        return new ValueTask<byte[]>(buffer);
    }

    public override ValueTask<ushort> ReadUInt16()
    {
        var value = _accessor.ReadUInt16(_position);
        _position += sizeof(ushort);

        return new ValueTask<ushort>(value);
    }

    public override ValueTask<short> ReadInt16()
    {
        var value = _accessor.ReadInt16(_position);
        _position += sizeof(short);

        return new ValueTask<short>(value);
    }

    public override ValueTask<uint> ReadUInt32()
    {
        var value = _accessor.ReadUInt32(_position);
        _position += sizeof(uint);

        return new ValueTask<uint>(value);
    }

    public override ValueTask<ulong> ReadUInt64()
    {
        var value = _accessor.ReadUInt64(_position);
        _position += sizeof(ulong);

        return new ValueTask<ulong>(value);
    }

    private void ReadCore(Span<byte> buffer)
    {
        unsafe
        {
            byte* ptr = null;

            try
            {
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                var ptrSrc = _accessor.PointerOffset + ptr + _position;

                fixed (byte* ptrDst = buffer)
                {
                    Buffer.MemoryCopy(ptrSrc, ptrDst, buffer.Length, buffer.Length);
                }
            }
            finally
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        _position += buffer.Length;
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

using System.IO.MemoryMappedFiles;

namespace PureHDF.VFD;

// AcquirePointer: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L120-L138
// Read single element: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryAccessor.cs#L148

// Does it make sense to acquire pointer only once instead of every read operation?
// https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround
// -> I think the synchrnonization complaint is not valid anymore: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L27-L28
internal unsafe partial class H5MemoryMappedFileDriver : H5DriverBase
{
    private readonly ThreadLocal<long> _position = new();
    private readonly MemoryMappedViewAccessor _accessor;

    public H5MemoryMappedFileDriver(MemoryMappedViewAccessor accessor)
    {
        _accessor = accessor;
    }

    public override long Position { get => _position.Value; }

    public override long Length => _accessor.Capacity;

    public override void Seek(long offset, SeekOrigin seekOrigin)
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
    }

    public override void ReadDataset(Memory<byte> buffer)
    {
        ReadCore(buffer);
    }

    public override ValueTask ReadDatasetAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        throw new Exception("Memory-mapped files cannot be accessed asynchronously.");
    }

    public override void Read(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override byte ReadByte()
    {
        var value = _accessor.ReadByte(_position.Value);
        _position.Value += sizeof(byte);

        return value;
    }

    public override byte[] ReadBytes(int count)
    {
        var buffer = new byte[count];
        ReadCore(buffer);

        return buffer;
    }

    public override ushort ReadUInt16()
    {
        var value = _accessor.ReadUInt16(_position.Value);
        _position.Value += sizeof(ushort);

        return value;
    }

    public override short ReadInt16()
    {
        var value = _accessor.ReadInt16(_position.Value);
        _position.Value += sizeof(short);

        return value;
    }

    public override uint ReadUInt32()
    {
        var value = _accessor.ReadUInt32(_position.Value);
        _position.Value += sizeof(uint);

        return value;
    }

    public override ulong ReadUInt64()
    {
        var value = _accessor.ReadUInt64(_position.Value);
        _position.Value += sizeof(ulong);

        return value;
    }

    private void ReadCore(Memory<byte> buffer)
    {
        unsafe
        {
            byte* ptr = null;

            try
            {
                _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                var ptrSrc = _accessor.PointerOffset + ptr + _position.Value;

                fixed (byte* ptrDst = buffer.Span)
                {
                    Buffer.MemoryCopy(ptrSrc, ptrDst, buffer.Length, buffer.Length);
                }
            }
            finally
            {
                _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        _position.Value += buffer.Length;
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
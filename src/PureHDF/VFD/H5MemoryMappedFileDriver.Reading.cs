using System.IO.MemoryMappedFiles;

namespace PureHDF.VFD;

// AcquirePointer: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L120-L138
// Read single element: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryAccessor.cs#L148

// Does it make sense to acquire pointer only once instead of every read operation?
// https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround
// -> I think the synchrnonization complaint is not valid anymore: https://github.com/dotnet/runtime/blob/9b76c28567640e4cbe0d20e18b765b8f1a47473f/src/libraries/System.Private.CoreLib/src/System/Runtime/InteropServices/SafeBuffer.cs#L27-L28
//
// A memory-mapped view is a genuinely synchronous source, so every member here returns an
// already-completed ValueTask. Sync-completing ValueTasks do not allocate, which is the point of an
// async-first read path: remote sources get real async, local ones pay nothing.
// CONCURRENCY MODEL: a driver instance is owned by one logical reader - the cursor is a plain
// field, because a ThreadLocal<long> reads back as 0 once an async continuation resumes on another
// thread. Parallelism does not require one reader per thread though: the read path allocates a
// per-operation driver over this same accessor (see CreateOperationDriverCore below), so a dataset
// resolved once can be read concurrently through a single shared H5File.
internal unsafe partial class H5MemoryMappedFileDriver : H5DriverBase
{
    private long _position;
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly bool _leaveOpen;

    public H5MemoryMappedFileDriver(MemoryMappedViewAccessor accessor)
        : this(accessor, leaveOpen: false)
    {
        //
    }

    private H5MemoryMappedFileDriver(MemoryMappedViewAccessor accessor, bool leaveOpen)
    {
        _accessor = accessor;
        _leaveOpen = leaveOpen;
    }

    // CONCURRENCY: every read here is absolute - the accessor is addressed by _position, never by
    // a cursor of its own - and the accessor's own reads may be issued concurrently
    // (SafeBuffer.AcquirePointer/ReleasePointer refcount with interlocked operations). So a second
    // driver over the same accessor, carrying its own _position, is correct.
    //
    // leaveOpen: true is what makes disposal safe. Without it Dispose would dispose the accessor,
    // and an operation driver doing that would kill the shared accessor and every other reader, the
    // file-level driver included.
    protected override H5DriverBase CreateOperationDriverCore()
    {
        return new H5MemoryMappedFileDriver(_accessor, leaveOpen: true);
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

    public override ValueTask ReadDatasetAsync(Memory<byte> buffer)
    {
        ReadCore(buffer.Span);

        return default;
    }

    // A memory-mapped view is a synchronous source by construction.
    public override bool TryReadDatasetSync(Span<byte> buffer)
    {
        ReadCore(buffer);

        return true;
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
                if (!_leaveOpen)
                    _accessor.Dispose();
            }

            _disposedValue = true;
        }
    }
}

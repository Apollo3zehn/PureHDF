using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PureHDF.VFD;

// CONCURRENCY MODEL (async-first): a driver instance is owned by exactly one logical reader and
// must not be shared across concurrent readers. This replaces the previous model, where a single
// driver was shared and the cursor lived in a ThreadLocal<long>.
//
// The change is forced, not stylistic: with async reads a continuation may resume on a different
// thread, where the ThreadLocal cursor reads back as 0. The result was silent corruption - the
// superblock scan desynced and every file reported "not a valid HDF 5 file".
//
// SYNCHRONOUS COMPLETION: a local file is not an asynchronous source. .NET on Unix has no true
// async file I/O, and the FileStream behind this handle is opened without FileOptions.Asynchronous,
// so RandomAccess.ReadAsync would queue every read - including a 2-byte metadata read - onto the
// thread pool and genuinely suspend. That cost is what made metadata-heavy reads 40-60% slower.
//
// So the reads below stay synchronous and hand back an already-completed ValueTask. That is exactly
// what ValueTask is for: the async signature is honored, no state machine is ever created, no
// continuation is ever boxed, and the methods stay inlineable. Drivers over genuinely remote
// sources (H5StreamDriver over an HTTP range-request stream) are the ones that actually suspend.
internal partial class H5FileHandleDriver : H5DriverBase
{
    private long _position;
    private readonly FileStream _stream; // it is important to keep a reference, otherwise the SafeFileHandle gets closed during the next GC
    private readonly SafeFileHandle _handle;
    private readonly bool _leaveOpen;

    public H5FileHandleDriver(FileStream stream, bool leaveOpen)
    {
        _stream = stream;
        _handle = _stream.SafeFileHandle;
        _leaveOpen = leaveOpen;
    }

    public override long Position { get => _position; }

    public override long Length => _stream.Length;

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

    // A local file handle always reads synchronously (see the note above), so the decode path can
    // take the Span overload and skip the CastMemoryManager allocation entirely.
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
        return new ValueTask<byte>(ReadScalar<byte>());
    }

    public override ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        ReadCore(buffer);

        return new ValueTask<byte[]>(buffer);
    }

    public override ValueTask<ushort> ReadUInt16()
    {
        return new ValueTask<ushort>(ReadScalar<ushort>());
    }

    public override ValueTask<short> ReadInt16()
    {
        return new ValueTask<short>(ReadScalar<short>());
    }

    public override ValueTask<uint> ReadUInt32()
    {
        return new ValueTask<uint>(ReadScalar<uint>());
    }

    public override ValueTask<ulong> ReadUInt64()
    {
        return new ValueTask<ulong>(ReadScalar<ulong>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadCore(Span<byte> buffer)
    {
        var count = RandomAccess.Read(_handle, buffer, _position);

        if (count != buffer.Length)
            throw new Exception("The file is too small");

        _position += count;
    }

    // No `await` in this method, so `stackalloc` and `Unsafe.SizeOf<T>` (a JIT constant) are both
    // available again - this is the baseline implementation, unchanged.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T ReadScalar<T>() where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        Span<byte> buffer = stackalloc byte[size];
        RandomAccess.Read(_handle, buffer, _position);
        _position += size;

        return MemoryMarshal.Cast<byte, T>(buffer)[0];
    }

    private bool _disposedValue;

    protected override void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (!_leaveOpen)
                    _stream.Dispose();
            }

            _disposedValue = true;
        }

        base.Dispose(disposing);
    }
}

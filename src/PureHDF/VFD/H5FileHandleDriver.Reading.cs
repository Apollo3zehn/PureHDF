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
// Isolation is per *operation*, not per reader: the read path allocates a driver per read operation
// over this same file handle (CreateOperationDriverCore below), so a dataset or attribute resolved
// once can be read concurrently through a single shared H5File. Object navigation
// (file.Dataset("x"), attribute enumeration) still moves the file-level cursor and remains
// single-threaded - resolve first, then read in parallel.
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

    // COALESCING: RandomAccess.Read below is POSITIONAL, so it bypasses the FileStream's buffer
    // entirely, so without the window a one-byte metadata field is a genuine pread - reading a
    // thousand dense attributes costs ~363,000 syscalls. One window per driver and a driver has one
    // logical reader, so it needs no synchronization - see ReadAheadWindow.
    private readonly ReadAheadWindow _readAhead = new();

    public H5FileHandleDriver(FileStream stream, bool leaveOpen)
    {
        _stream = stream;
        _handle = _stream.SafeFileHandle;
        _leaveOpen = leaveOpen;
    }

    private H5FileHandleDriver(FileStream stream, SafeFileHandle handle, bool leaveOpen)
    {
        _stream = stream;
        _handle = handle;
        _leaveOpen = leaveOpen;
    }

    // CONCURRENCY: reads go through RandomAccess.Read(_handle, buffer, _position), which is
    // positionless - it never touches the FileStream's own cursor. So a second driver over the
    // very same SafeFileHandle, carrying its own _position, reads correctly and needs no extra
    // file descriptor.
    //
    // leaveOpen: true, because the FileStream belongs to the H5File and outlives the operation.
    // The already-captured _handle is passed straight through rather than re-read from
    // stream.SafeFileHandle: that getter flushes the FileStream, and two threads starting an
    // operation at the same time would then be doing that concurrently on shared buffer state.
    protected override H5DriverBase CreateOperationDriverCore()
    {
        return new H5FileHandleDriver(_stream, _handle, leaveOpen: true);
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
        ReadMetadataCore(buffer);

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

    // Bulk payload: straight into the caller's buffer, never through the read-ahead window. A chunk
    // is large and decoded once, so buffering it would only displace the structure the window holds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadCore(Span<byte> buffer)
    {
        var count = RandomAccess.Read(_handle, buffer, _position);

        if (count != buffer.Length)
            throw new Exception("The file is too small");

        _position += count;
    }

    // Every STRUCTURAL read funnels through here, which is what makes one window enough to cover the
    // whole decode of an object header, a b-tree node or an attribute message.
    private void ReadMetadataCore(Span<byte> buffer)
    {
        if (_readAhead.TryServe(_position, buffer))
        {
            _position += buffer.Length;

            return;
        }

        var refillLength = _readAhead.GetRefillLength(buffer.Length, _position, Length);

        if (refillLength == 0)
        {
            ReadCore(buffer);

            return;
        }

        var window = _readAhead.BeginRefill(refillLength);
        var read = RandomAccess.Read(_handle, window.Span, _position);

        // A short pread is legal even away from the end of the file, so what was actually delivered
        // becomes the window rather than what was asked for. Only a read too short to satisfy the
        // CALLER is an error, and it is the same error ReadCore reports.
        if (read < buffer.Length)
            throw new Exception("The file is too small");

        _readAhead.CompleteRefill(_position, read);

        // The refill covers the caller by construction (GetRefillLength never returns less than
        // `count`, and the short-read check above enforces it), so this cannot legitimately fail. It
        // is still checked, because the failure mode of ignoring it is a silently UNFILLED buffer -
        // the caller would decode uninitialized memory rather than see an error.
        if (!_readAhead.TryServe(_position, buffer))
            throw new Exception("The read-ahead window failed to serve a read it had just been filled for.");

        _position += buffer.Length;
    }

    // No `await` in this method, so `stackalloc` and `Unsafe.SizeOf<T>` (a JIT constant) are both
    // available.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T ReadScalar<T>() where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        Span<byte> buffer = stackalloc byte[size];
        ReadMetadataCore(buffer);

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

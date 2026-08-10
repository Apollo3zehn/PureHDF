using System.Buffers;
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
// Isolating per reader instead of per thread costs almost nothing here, because the actual reads go
// through RandomAccess.ReadAsync(handle, buffer, offset), which is stateless - the cursor below is
// just bookkeeping. Callers that want parallel reads open one reader per thread.
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
        return ReadCore(buffer);
    }

    public override ValueTask Read(Memory<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask<byte> ReadByte()
    {
        return await ReadScalar<byte>().ConfigureAwait(false);
    }

    public override async ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        await ReadCore(buffer).ConfigureAwait(false);

        return buffer;
    }

    public override async ValueTask<ushort> ReadUInt16()
    {
        return await ReadScalar<ushort>().ConfigureAwait(false);
    }

    public override async ValueTask<short> ReadInt16()
    {
        return await ReadScalar<short>().ConfigureAwait(false);
    }

    public override async ValueTask<uint> ReadUInt32()
    {
        return await ReadScalar<uint>().ConfigureAwait(false);
    }

    public override async ValueTask<ulong> ReadUInt64()
    {
        return await ReadScalar<ulong>().ConfigureAwait(false);
    }

    private async ValueTask ReadCore(Memory<byte> buffer)
    {
        var count = await RandomAccess.ReadAsync(_handle, buffer, Position).ConfigureAwait(false);

        if (count != buffer.Length)
            throw new Exception("The file is too small");

        _position += count;
    }

    // Was: stackalloc + MemoryMarshal.Cast — a Span cannot cross an await.
    private async ValueTask<T> ReadScalar<T>() where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        using var owner = MemoryPool<byte>.Shared.Rent(size);
        var buffer = owner.Memory[..size];

        await RandomAccess.ReadAsync(_handle, buffer, Position).ConfigureAwait(false);
        _position += size;

        return MemoryMarshal.Cast<byte, T>(buffer.Span)[0];
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

using System.Buffers;
using System.Runtime.InteropServices;

namespace PureHDF.VFD;

internal partial class H5StreamDriver : H5DriverBase
{
    private readonly bool _leaveOpen;
    private readonly Stream _stream;

    public H5StreamDriver(Stream stream, bool leaveOpen)
    {
        if (!stream.CanRead)
            throw new Exception("The stream must be readable.");

        if (!stream.CanSeek)
            throw new Exception("The stream must be seekable.");

        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    public override long Position { get => _stream.Position; }

    public override long Length => _stream.Length;

    public override void Seek(long offset, SeekOrigin seekOrigin)
    {
        switch (seekOrigin)
        {
            case SeekOrigin.Begin:
                _stream.Seek(offset, SeekOrigin.Begin);
                break;

            case SeekOrigin.Current:
                _stream.Seek(offset, SeekOrigin.Current);
                break;

            default:
                throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
        };
    }

    public override async ValueTask ReadDataset(Memory<byte> buffer)
    {
        if (_stream is IDatasetStream datasetStream)
            await datasetStream.ReadDataset(buffer).ConfigureAwait(false);

        // ReadExactlyAsyncCompat, never a hand-rolled loop: it advances the buffer it reads into and
        // treats a zero-byte read as end of stream. Getting either wrong is silent data corruption on
        // any stream that returns partial reads.
        else
            await _stream.ReadExactlyAsyncCompat(buffer).ConfigureAwait(false);
    }

    public override ValueTask Read(Memory<byte> buffer)
    {
        return _stream.ReadExactlyAsyncCompat(buffer);
    }

    public override async ValueTask<byte> ReadByte()
    {
        return await ReadScalar<byte>().ConfigureAwait(false);
    }

    public override async ValueTask<byte[]> ReadBytes(int count)
    {
        var buffer = new byte[count];
        await _stream.ReadExactlyAsyncCompat(buffer).ConfigureAwait(false);

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

    // Was: stackalloc + MemoryMarshal.Cast. A Span cannot cross an await, so the scratch
    // buffer now comes from the shared pool. This is the per-scalar-read cost of async-first.
    private async ValueTask<T> ReadScalar<T>() where T : unmanaged
    {
        var size = Marshal.SizeOf<T>();
        using var owner = MemoryPool<byte>.Shared.Rent(size);
        var buffer = owner.Memory[..size];

        await _stream.ReadExactlyAsyncCompat(buffer).ConfigureAwait(false);

        return MemoryMarshal.Cast<byte, T>(buffer.Span)[0];
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

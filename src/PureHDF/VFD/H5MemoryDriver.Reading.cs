using System.Runtime.InteropServices;

namespace PureHDF.VFD;

// A ReadOnlyMemory<byte> source is genuinely synchronous: every read is a Span slice + CopyTo,
// never a syscall. Sync-completing ValueTasks do not allocate, which is the point of an
// async-first read path: remote sources get real async, local ones pay nothing.
// CONCURRENCY MODEL: a driver instance is owned by one logical reader - the cursor is a plain
// field. Parallelism does not require one reader per thread: the read path allocates a
// per-operation driver over this same buffer (see CreateOperationDriverCore below), so a dataset
// resolved once can be read concurrently through a single shared H5File.
internal partial class H5MemoryDriver : H5DriverBase
{
    private long _position;
    private readonly ReadOnlyMemory<byte> _source;

    public H5MemoryDriver(ReadOnlyMemory<byte> source)
    {
        _source = source;
    }

    // CONCURRENCY: every read here is absolute - the buffer is addressed by _position, never by
    // a cursor of its own - and a ReadOnlyMemory<byte> is safe to read concurrently (each driver
    // slices the same underlying buffer at its own _position). So a second driver over the same
    // buffer, carrying its own _position, is correct.
    //
    // There is no resource to leave open: the caller owns the buffer. So unlike the memory-mapped
    // and stream drivers there is no leaveOpen flag and nothing to dispose beyond the base class.
    protected override H5DriverBase CreateOperationDriverCore()
    {
        return new H5MemoryDriver(_source);
    }

    public override long Position { get => _position; }

    public override long Length => _source.Length;

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

    // An in-memory buffer is a synchronous source by construction.
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
        var value = _source.Span[(int)_position];
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
        var value = MemoryMarshal.Read<ushort>(_source.Span[(int)_position..]);
        _position += sizeof(ushort);

        return new ValueTask<ushort>(value);
    }

    public override ValueTask<short> ReadInt16()
    {
        var value = MemoryMarshal.Read<short>(_source.Span[(int)_position..]);
        _position += sizeof(short);

        return new ValueTask<short>(value);
    }

    public override ValueTask<uint> ReadUInt32()
    {
        var value = MemoryMarshal.Read<uint>(_source.Span[(int)_position..]);
        _position += sizeof(uint);

        return new ValueTask<uint>(value);
    }

    public override ValueTask<ulong> ReadUInt64()
    {
        var value = MemoryMarshal.Read<ulong>(_source.Span[(int)_position..]);
        _position += sizeof(ulong);

        return new ValueTask<ulong>(value);
    }

    private void ReadCore(Span<byte> buffer)
    {
        _source.Span.Slice((int)_position, buffer.Length).CopyTo(buffer);
        _position += buffer.Length;
    }
}

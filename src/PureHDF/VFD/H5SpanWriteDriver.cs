namespace PureHDF.VFD;

/// <summary>
/// A write-only driver over a fixed, caller-owned buffer, for encoding a structure in memory before
/// it is written to the file in one piece.
/// </summary>
/// <remarks>
/// Exists so that a checksummed structure can be checksummed from the bytes just encoded. Writing it
/// to the file first and reading it back would cost an async read per structure and flush a buffered
/// stream's write buffer every time.
/// <para>
/// Positions are the file positions the bytes will be written at, not offsets into the buffer:
/// <paramref name="origin" /> is where the buffer's first byte goes. An encoder that records its own
/// position to patch later (a chunked layout's storage properties do) then records the right one.
/// </para>
/// </remarks>
/// <param name="buffer">Receives the encoded bytes.</param>
/// <param name="origin">The file position, as the file's driver reports positions, of the buffer's first byte.</param>
internal sealed class H5SpanWriteDriver(Memory<byte> buffer, long origin) : H5DriverBase
{
    private long _offset;

    public override long Position => origin + _offset;

    public override long Length => origin + buffer.Length;

    public override void Seek(long offset, SeekOrigin seekOrigin)
    {
        _offset = seekOrigin switch
        {
            SeekOrigin.Begin => offset - origin,
            SeekOrigin.Current => _offset + offset,
            _ => throw new Exception($"Seek origin '{seekOrigin}' is not supported.")
        };
    }

    public override void Write(Span<byte> data)
    {
        data.CopyTo(buffer.Span[(int)_offset..]);
        _offset += data.Length;
    }

    public override void WriteDataset(Span<byte> data) => Write(data);

    public override void SetLength(long endAddress) => throw new NotSupportedException();

    public override ValueTask ReadDatasetAsync(Memory<byte> destination) => throw new NotSupportedException();

    public override ValueTask Read(Memory<byte> destination) => throw new NotSupportedException();

    public override ValueTask<byte> ReadByte() => throw new NotSupportedException();

    public override ValueTask<byte[]> ReadBytes(int count) => throw new NotSupportedException();

    public override ValueTask<ushort> ReadUInt16() => throw new NotSupportedException();

    public override ValueTask<short> ReadInt16() => throw new NotSupportedException();

    public override ValueTask<uint> ReadUInt32() => throw new NotSupportedException();

    public override ValueTask<ulong> ReadUInt64() => throw new NotSupportedException();
}

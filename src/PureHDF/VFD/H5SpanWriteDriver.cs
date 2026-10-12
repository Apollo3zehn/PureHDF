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

/// <summary>
/// Writes a structure whose last four bytes are a Jenkins lookup3 checksum over everything before
/// them, computing the checksum from the encoded bytes rather than reading them back from the file.
/// </summary>
internal static class ChecksummedStructure
{
    /// <summary>
    /// Encodes <paramref name="encodeSize" /> bytes at <paramref name="driver" />'s position:
    /// <paramref name="encodeBody" /> writes everything but the checksum, which is appended.
    /// </summary>
    /// <remarks>
    /// Reading the bytes back would cost a read per structure and need a stream that can be read while it
    /// is written - which a forward-only upload, holding only the part it is filling, cannot be.
    /// </remarks>
    public static void Write(H5DriverBase driver, int encodeSize, Action<H5DriverBase> encodeBody)
        => Write(driver, encodeSize, encodeBody, static (driver, encodeBody) => encodeBody(driver));

    /// <summary>
    /// <see cref="Write(H5DriverBase, int, Action{H5DriverBase})" /> with the body's state passed in, so a
    /// caller encoding many structures can use a static body and allocate no closure per structure.
    /// </summary>
    public static void Write<TState>(H5DriverBase driver, int encodeSize, TState state, Action<H5DriverBase, TState> encodeBody)
    {
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(encodeSize);

        try
        {
            var buffer = rented.AsMemory(0, encodeSize);

            // Cleared because a pooled array holds whatever its last user left, and anything an encoder
            // skips over rather than writes would otherwise end up in the file.
            buffer.Span.Clear();

            WriteToBuffer(buffer, driver.Position, state, encodeBody);
            driver.Write(buffer.Span);
        }

        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Encodes into <paramref name="buffer" />, which is the whole structure including its checksum, and
    /// fills in the checksum. <paramref name="origin" /> is the file position of the buffer's first byte.
    /// </summary>
    public static void WriteToBuffer<TState>(Memory<byte> buffer, long origin, TState state, Action<H5DriverBase, TState> encodeBody)
    {
        using var spanDriver = new H5SpanWriteDriver(buffer, origin);

        encodeBody(spanDriver, state);
        UpdateChecksum(buffer.Span);
    }

    /// <summary>Recomputes the checksum in the last four bytes of <paramref name="structure" />.</summary>
    public static void UpdateChecksum(Span<byte> structure)
    {
        var checksum = ChecksumUtils.JenkinsLookup3(structure[..^sizeof(uint)]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(structure[^sizeof(uint)..], checksum);
    }
}

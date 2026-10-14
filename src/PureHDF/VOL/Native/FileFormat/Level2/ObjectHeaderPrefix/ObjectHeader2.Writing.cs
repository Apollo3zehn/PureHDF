using PureHDF.VFD;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    // Encoded into a buffer and checksummed from it, then written to the file in one piece. Reading the
    // header back from the file for its checksum would cost a read per object header, and flush a
    // buffered stream's write buffer each time.
    /// <param name="context">The write context.</param>
    /// <param name="encodeSize">The size of the encoded header.</param>
    public ulong Encode(NativeWriteContext context, out int encodeSize)
    {
        var address = Allocate(context, out encodeSize, out var headerMessagesEncodeSize);

        context.Driver.SeekRelativeToBaseAddress(address);
        ChecksummedStructure.Write(
            context.Driver,
            encodeSize,
            (Header: this, headerMessagesEncodeSize),
            static (driver, state) => state.Header.EncodeCore(driver, state.headerMessagesEncodeSize));

        return (ulong)address;
    }

    /// <summary>
    /// Allocates the header like <see cref="Encode(NativeWriteContext, out int)" />, and returns the encoded bytes.
    /// </summary>
    /// <remarks>
    /// For a header that may have to be patched after it is encoded - a chunked dataset's, once its index
    /// is known. Patching a copy needs no read of the file, which a forward-only stream cannot serve.
    /// </remarks>
    /// <param name="context">The write context.</param>
    /// <param name="write">
    /// Whether to write the header now. A header that will be patched is better allocated now and written
    /// once it is final, since rewriting it would change bytes a destination may already have shipped.
    /// </param>
    public (ulong Address, byte[] Encoded) EncodeAndKeep(NativeWriteContext context, bool write)
    {
        var address = (ulong)context.FreeSpaceManager.Allocate(GetEncodeSize(), AllocationKind.Metadata);
        var encoded = EncodeAt(context, address);

        if (write)
        {
            context.Driver.SeekRelativeToBaseAddress((long)address);
            context.Driver.Write(encoded);
        }

        return (address, encoded);
    }

    /// <summary>
    /// Encodes the header as if at <paramref name="address" />, allocated by the caller or not yet at all,
    /// and returns the encoded bytes without writing them. Nothing in a header depends on where it is, but
    /// positions recorded while encoding it are relative to that address.
    /// </summary>
    public byte[] EncodeAt(NativeWriteContext context, ulong address)
    {
        var headerMessagesEncodeSize = GetHeaderMessagesEncodeSize();
        var encoded = new byte[GetEncodeSize(headerMessagesEncodeSize)];

        ChecksummedStructure.WriteToBuffer(
            encoded,
            origin: (long)context.Driver.BaseAddress + (long)address,
            (Header: this, headerMessagesEncodeSize),
            static (driver, state) => state.Header.EncodeCore(driver, state.headerMessagesEncodeSize));

        return encoded;
    }

    /// <summary>
    /// The size of the encoded header.
    /// </summary>
    public int GetEncodeSize()
        => (int)GetEncodeSize(GetHeaderMessagesEncodeSize());

    private long Allocate(NativeWriteContext context, out int encodeSize, out ulong headerMessagesEncodeSize)
    {
        headerMessagesEncodeSize = GetHeaderMessagesEncodeSize();
        encodeSize = (int)GetEncodeSize(headerMessagesEncodeSize);

        return context.FreeSpaceManager.Allocate(encodeSize, AllocationKind.Metadata);
    }

    private void EncodeCore(H5DriverBase driver, ulong headerMessagesEncodeSize)
    {
        // signature
        driver.Write(Signature);

        // version
        driver.Write(Version);

        // flags
        driver.Write((byte)Flags);

        // access time, modification time, change time and birth time
        if (Flags.HasFlag(ObjectHeaderFlags.StoreFileAccessTimes))
        {
            driver.Write(AccessTime);
            driver.Write(ModificationTime);
            driver.Write(ChangeTime);
            driver.Write(BirthTime);
        }

        // maximum compact attributes count and minimum dense attributes count
        if (Flags.HasFlag(ObjectHeaderFlags.StoreNonDefaultAttributePhaseChangeValues))
        {
            driver.Write(MaximumCompactAttributesCount);
            driver.Write(MinimumDenseAttributesCount);
        }

        // size of chunk 0
        // TODO: this size should depend on actual value of headerMessagesEncodeSize
        var chunkFieldSize = GetChunk0FieldSize();
        WriteUtils.WriteUlongArbitrary(driver, headerMessagesEncodeSize, chunkFieldSize);

        // with creation order
        var withCreationOrder = Flags.HasFlag(ObjectHeaderFlags.TrackAttributeCreationOrder);

        // header messages
        WriteHeaderMessages(driver, withCreationOrder);
    }

    public void WriteHeaderMessages(H5DriverBase driver, bool withCreationOrder)
    {
        foreach (var message in HeaderMessages)
        {
            message.Encode(driver, withCreationOrder);
        }
    }

    private ulong GetHeaderMessagesEncodeSize()
    {
        var withCreationOrder = Flags.HasFlag(ObjectHeaderFlags.TrackAttributeCreationOrder);

        return HeaderMessages
            .Aggregate(0UL, (result, headerMessage) => result + headerMessage.GetEncodeSize(withCreationOrder));
    }

    private ulong GetEncodeSize(ulong headerMessagesEncodeSize)
    {
        return
            (uint)Signature.Length +
            sizeof(byte) +
            sizeof(byte) +
            (
                Flags.HasFlag(ObjectHeaderFlags.StoreFileAccessTimes)
                    ? sizeof(uint) + sizeof(uint) + sizeof(uint) + sizeof(uint)
                    : 0UL
            ) +
            (
                Flags.HasFlag(ObjectHeaderFlags.StoreNonDefaultAttributePhaseChangeValues)
                    ? sizeof(ushort) + sizeof(ushort)
                    : 0UL
            ) +
            GetChunk0FieldSize() +
            headerMessagesEncodeSize +
            0UL /* gap */ +
            sizeof(uint);
    }

    private byte GetChunk0FieldSize()
    {
        return (byte)(1 << ((byte)Flags & 0x03));
    }
}
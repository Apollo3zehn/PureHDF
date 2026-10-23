using PureHDF.VFD;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    // Encoded into a buffer and checksummed from it, then written to the file in one piece. Writing
    // straight to the file and reading the header back for its checksum would cost an async read per
    // object header, and flush a buffered stream's write buffer each time.
    //
    // Completes synchronously; its callers in H5NativeWriter block on the ValueTask it returns.
    public ValueTask<ulong> Encode(NativeWriteContext context)
        => new(Encode(context, out _));

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
    /// Encodes the header like <see cref="Encode(NativeWriteContext, out int)" />, and also returns the encoded bytes.
    /// </summary>
    /// <remarks>
    /// For a header that has to be patched after it is written - a chunked dataset's, once its index is
    /// known. Patching a copy and writing it again needs no read of the file, which a forward-only
    /// stream cannot serve.
    /// </remarks>
    public (ulong Address, byte[] Encoded) EncodeAndKeep(NativeWriteContext context)
    {
        var address = Allocate(context, out var encodeSize, out var headerMessagesEncodeSize);
        var encoded = new byte[encodeSize];

        ChecksummedStructure.WriteToBuffer(
            encoded,
            origin: (long)context.Driver.BaseAddress + address,
            (Header: this, headerMessagesEncodeSize),
            static (driver, state) => state.Header.EncodeCore(driver, state.headerMessagesEncodeSize));

        context.Driver.SeekRelativeToBaseAddress(address);
        context.Driver.Write(encoded);

        return ((ulong)address, encoded);
    }

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
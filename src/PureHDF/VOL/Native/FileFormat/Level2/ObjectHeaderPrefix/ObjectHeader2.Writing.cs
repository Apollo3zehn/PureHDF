using System.Buffers;
using PureHDF.VFD;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    // Encoded into a pooled buffer and checksummed from it, then written to the file in one piece.
    // Writing straight to the file and reading the header back for its checksum cost an async read
    // per object header, and flushed a buffered stream's write buffer each time.
    //
    // Completes synchronously; the ValueTask keeps the signature its callers already bridge.
    public ValueTask<ulong> Encode(NativeWriteContext context)
    {
        var headerMessagesEncodeSize = GetHeaderMessagesEncodeSize();
        var encodeSize = (int)GetEncodeSize(headerMessagesEncodeSize);

        var freeSpaceManager = context.FreeSpaceManager;
        var address = freeSpaceManager.Allocate(encodeSize);

        var rented = ArrayPool<byte>.Shared.Rent(encodeSize);

        try
        {
            var buffer = rented.AsMemory(0, encodeSize);

            // Cleared because a pooled array holds whatever its last user left, and anything an encoder
            // skips over rather than writes would otherwise end up in the file.
            buffer.Span.Clear();

            using var driver = new H5SpanWriteDriver(
                buffer,
                origin: (long)context.Driver.BaseAddress + address);

            EncodeCore(driver, headerMessagesEncodeSize);

            var checksumData = buffer.Span[..(encodeSize - sizeof(uint))];
            var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

            driver.Write(checksum);

            context.Driver.SeekRelativeToBaseAddress(address);
            context.Driver.Write(buffer.Span);
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return new ValueTask<ulong>((ulong)address);
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
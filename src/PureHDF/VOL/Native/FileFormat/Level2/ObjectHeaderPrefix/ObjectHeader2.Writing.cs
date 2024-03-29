﻿using System.Buffers;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    public ulong Encode(NativeWriteContext context)
    {
        var headerMessagesEncodeSize = GetHeaderMessagesEncodeSize();
        var encodeSize = GetEncodeSize(headerMessagesEncodeSize);

        var freeSpaceManager = context.FreeSpaceManager;
        var address = freeSpaceManager.Allocate((long)encodeSize);

        var driver = context.Driver;
        driver.Seek(address, SeekOrigin.Begin);

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

        // checksum
        var encodeSizeWithoutChecksum = (int)(encodeSize - sizeof(uint));
        using var buffer = MemoryPool<byte>.Shared.Rent(encodeSizeWithoutChecksum);
        var checksumData = buffer.Memory.Span[..encodeSizeWithoutChecksum];

        driver.Seek(address, SeekOrigin.Begin);
        driver.Read(checksumData);

        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);

        return (ulong)address;
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
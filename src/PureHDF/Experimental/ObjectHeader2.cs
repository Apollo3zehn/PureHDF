using PureHDF.Experimental;

namespace PureHDF.VOL.Native;

internal partial record class ObjectHeader2
{
    public void Encode(WriteContext writeContext)
    {
        var freeSpaceManager = writeContext.FreeSpaceManager;
        // TODO reserve space here and remove the 1024 byte reserve call

        var driver = writeContext.Driver;

        var position1 = driver.BaseStream.Position;

        driver.Write(Signature);
        driver.Write(Version);
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

        // size of chunk 0 (fake)
        var position2 = driver.BaseStream.Position;
        var chunkFieldSize = (byte)(1 << ((byte)Flags & 0x03));
        WriteUtils.WriteUlongArbitrary(driver, 0, chunkFieldSize);

        // with creation order
        var withCreationOrder = Flags.HasFlag(ObjectHeaderFlags.TrackAttributeCreationOrder);

        // header messages
        var position3 = driver.BaseStream.Position;
        WriteHeaderMessages(driver, withCreationOrder);
        var position4 = driver.BaseStream.Position;

        // size of chunk 0 (real)
        driver.BaseStream.Seek(position2, SeekOrigin.Begin);
        var sizeOfChunk0 = (ulong)(position4 - position3);
        WriteUtils.WriteUlongArbitrary(driver, sizeOfChunk0, chunkFieldSize);

        // checksum
        driver.BaseStream.Seek(position1, SeekOrigin.Begin);
        var checksumData = new byte[position4 - position1];
        driver.BaseStream.Read(checksumData);
        var checksum = ChecksumUtils.JenkinsLookup3(checksumData);

        driver.Write(checksum);
    }

    public void WriteHeaderMessages(BinaryWriter driver, bool withCreationOrder)
    {
        foreach (var message in HeaderMessages)
        {
            message.Encode(driver, withCreationOrder);
        }
    }
}
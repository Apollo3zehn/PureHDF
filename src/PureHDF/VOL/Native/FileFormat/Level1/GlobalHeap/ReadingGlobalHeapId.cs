namespace PureHDF.VOL.Native;

internal readonly record struct ReadingGlobalHeapId(
    ulong CollectionAddress,
    uint ObjectIndex
)
{
    public static ReadingGlobalHeapId Decode(Superblock superblock, H5DriverBase localDriver)
    {
        return new ReadingGlobalHeapId(
            CollectionAddress: superblock.ReadOffset(localDriver),
            ObjectIndex: localDriver.ReadUInt32()
        );
    }
}
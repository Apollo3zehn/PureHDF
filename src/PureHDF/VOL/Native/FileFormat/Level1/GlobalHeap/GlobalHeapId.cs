namespace PureHDF.VOL.Native;

internal readonly record struct GlobalHeapId(
    ulong CollectionAddress,
    uint ObjectIndex
)
{
    public static GlobalHeapId Decode(Superblock superblock, H5DriverBase localDriver)
    {
        return new GlobalHeapId(
            CollectionAddress: superblock.ReadOffset(localDriver),
            ObjectIndex: localDriver.ReadUInt32()
        );
    }
}
namespace PureHDF.VOL.Native;

internal readonly record struct GlobalHeapId(
    NativeContext Context,
    ulong CollectionAddress,
    uint ObjectIndex
)
{
    public GlobalHeapCollection Collection
    {
        get
        {
            // TODO: Because Global Heap ID gets a brand new driver (from the attribute), it cannot be reused here. Is this a good approach?
            return NativeCache.GetGlobalHeapObject(Context, CollectionAddress);
        }
    }

    public static GlobalHeapId Decode(NativeContext context)
    {
        return new GlobalHeapId(
            Context: context,
            CollectionAddress: default,
            ObjectIndex: default
        );
    }

    public static GlobalHeapId Decode(NativeContext context, H5DriverBase localDriver)
    {
        var (_, superblock) = context;

        return new GlobalHeapId(
            Context: context,
            CollectionAddress: superblock.ReadOffset(localDriver),
            ObjectIndex: localDriver.ReadUInt32()
        );
    }
}
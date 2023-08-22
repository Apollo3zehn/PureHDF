namespace PureHDF.VOL.Native;

internal readonly record struct ExternalFileListSlot(
    ulong NameHeapOffset,
    ulong Offset,
    ulong Size
)
{
    public static ExternalFileListSlot Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        return new ExternalFileListSlot(
            NameHeapOffset: superblock.ReadLength(driver),
            Offset: superblock.ReadLength(driver),
            Size: superblock.ReadLength(driver)
        );
    }
}
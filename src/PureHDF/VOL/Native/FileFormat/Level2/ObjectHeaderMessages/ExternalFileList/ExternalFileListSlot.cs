namespace PureHDF.VOL.Native;

internal readonly record struct ExternalFileListSlot(
    ulong NameHeapOffset,
    ulong Offset,
    ulong Size
)
{
    public static async ValueTask<ExternalFileListSlot> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        var nameHeapOffset = await superblock.ReadLength(driver).ConfigureAwait(false);
        var offset = await superblock.ReadLength(driver).ConfigureAwait(false);
        var size = await superblock.ReadLength(driver).ConfigureAwait(false);

        return new ExternalFileListSlot(
            NameHeapOffset: nameHeapOffset,
            Offset: offset,
            Size: size
        );
    }
}
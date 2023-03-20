namespace PureHDF.VOL.Native;

internal class ExternalFileListSlot
{
    #region Constructors

    public ExternalFileListSlot(H5Context context)
    {
        var (driver, superblock) = context;

        // name heap offset
        NameHeapOffset = superblock.ReadLength(driver);

        // offset
        Offset = superblock.ReadLength(driver);

        // size
        Size = superblock.ReadLength(driver);
    }

    #endregion

    #region Properties

    public ulong NameHeapOffset { get; set; }
    public ulong Offset { get; set; }
    public ulong Size { get; set; }

    #endregion
}
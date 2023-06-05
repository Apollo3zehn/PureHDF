namespace PureHDF.VOL.Native;

internal class ContiguousStoragePropertyDescription : StoragePropertyDescription
{
    #region Constructors

    public ContiguousStoragePropertyDescription(NativeContext context)
    {
        var (driver, superblock) = context;

        Address = superblock.ReadOffset(driver);
        Size = superblock.ReadLength(driver);
    }

    #endregion

    #region Properties

    public ulong Size { get; set; }

    #endregion
}
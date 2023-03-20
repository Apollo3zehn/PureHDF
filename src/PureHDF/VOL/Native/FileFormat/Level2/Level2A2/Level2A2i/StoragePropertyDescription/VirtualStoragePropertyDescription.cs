namespace PureHDF.VOL.Native;

internal class VirtualStoragePropertyDescription : StoragePropertyDescription
{
    #region Constructors

    public VirtualStoragePropertyDescription(H5Context context)
    {
        var (driver, superblock) = context;

        // address
        Address = superblock.ReadOffset(driver);

        // index
        Index = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public uint Index { get; set; }

    #endregion
}
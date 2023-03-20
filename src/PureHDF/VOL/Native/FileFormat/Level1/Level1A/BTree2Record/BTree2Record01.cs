namespace PureHDF.VOL.Native;

internal struct BTree2Record01 : IBTree2Record
{
    #region Constructors

    public BTree2Record01(H5Context context)
    {
        var (driver, superblock) = context;

        HugeObjectAddress = superblock.ReadOffset(driver);
        HugeObjectLength = superblock.ReadLength(driver);
        HugeObjectId = superblock.ReadLength(driver);
    }

    #endregion

    #region Properties

    public ulong HugeObjectAddress { get; set; }
    public ulong HugeObjectLength { get; set; }
    public ulong HugeObjectId { get; set; }

    #endregion
}
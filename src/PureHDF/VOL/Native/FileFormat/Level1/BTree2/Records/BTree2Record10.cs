namespace PureHDF.VOL.Native;

internal struct BTree2Record10 : IBTree2Record
{
    #region Constructors

    public BTree2Record10(NativeContext context, byte rank)
    {
        var (driver, superblock) = context;

        // address
        Address = superblock.ReadOffset(driver);

        // scaled offsets
        ScaledOffsets = new ulong[rank];

        for (int i = 0; i < rank; i++)
        {
            ScaledOffsets[i] = driver.ReadUInt64();
        }
    }

    #endregion

    #region Properties

    public ulong Address { get; set; }
    public ulong[] ScaledOffsets { get; set; }

    #endregion
}
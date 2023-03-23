namespace PureHDF.VOL.Native;

internal struct BTree2Record11 : IBTree2Record
{
    #region Constructors

    public BTree2Record11(NativeContext context, byte rank, uint chunkSizeLength)
    {
        var (driver, superblock) = context;

        // address
        Address = superblock.ReadOffset(driver);

        // chunk size
        ChunkSize = Utils.ReadUlong(driver, chunkSizeLength);

        // filter mask
        FilterMask = driver.ReadUInt32();

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
    public ulong ChunkSize { get; set; }
    public uint FilterMask { get; set; }
    public ulong[] ScaledOffsets { get; set; }

    #endregion
}
namespace PureHDF.VOL.Native;

internal class ChunkedStoragePropertyDescription3 : ChunkedStoragePropertyDescription
{
    #region Constructors

    public ChunkedStoragePropertyDescription3(NativeContext context)
    {
        var (driver, superblock) = context;

        // rank
        Rank = driver.ReadByte();

        // address
        Address = superblock.ReadOffset(driver);

        // dimension sizes
        DimensionSizes = new uint[Rank];

        for (uint i = 0; i < Rank; i++)
        {
            DimensionSizes[i] = driver.ReadUInt32();
        }
    }

    #endregion

    #region Properties

    public uint[] DimensionSizes { get; set; }

    #endregion
}
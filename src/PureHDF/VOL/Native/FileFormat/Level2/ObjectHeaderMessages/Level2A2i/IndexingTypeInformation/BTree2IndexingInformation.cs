namespace PureHDF.VOL.Native;

internal class BTree2IndexingInformation : IndexingInformation
{
    #region Constructors

    public BTree2IndexingInformation(H5DriverBase driver)
    {
        // node size
        NodeSize = driver.ReadUInt32();

        // split percent
        SplitPercent = driver.ReadByte();

        // merge percent
        MergePercent = driver.ReadByte();
    }

    #endregion

    #region Properties

    public uint NodeSize { get; set; }
    public byte SplitPercent { get; set; }
    public byte MergePercent { get; set; }

    #endregion
}
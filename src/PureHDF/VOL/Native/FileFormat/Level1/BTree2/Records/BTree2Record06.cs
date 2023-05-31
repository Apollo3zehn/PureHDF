namespace PureHDF.VOL.Native;

internal struct BTree2Record06 : IBTree2Record
{
    #region Constructors

    public BTree2Record06(H5DriverBase driver)
    {
        CreationOrder = driver.ReadUInt64();
        HeapId = driver.ReadBytes(7);
    }

    #endregion

    #region Properties

    public ulong CreationOrder { get; set; }
    public byte[] HeapId { get; set; }

    #endregion
}
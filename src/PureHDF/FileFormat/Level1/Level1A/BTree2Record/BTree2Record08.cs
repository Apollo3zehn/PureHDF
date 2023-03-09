namespace PureHDF;

internal struct BTree2Record08 : IBTree2Record
{
    #region Constructors

    public BTree2Record08(H5DriverBase driver)
    {
        HeapId = driver.ReadBytes(8);
        MessageFlags = (MessageFlags)driver.ReadByte();
        CreationOrder = driver.ReadUInt32();
        NameHash = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public byte[] HeapId { get; set; }
    public MessageFlags MessageFlags { get; set; }
    public uint CreationOrder { get; set; }
    public uint NameHash { get; set; }

    #endregion
}
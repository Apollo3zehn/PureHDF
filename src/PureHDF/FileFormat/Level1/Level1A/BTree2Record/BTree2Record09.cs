namespace PureHDF
{
    internal struct BTree2Record09 : IBTree2Record
    {
        #region Constructors

        public BTree2Record09(H5DriverBase driver)
        {
            HeapId = driver.ReadBytes(8);
            MessageFlags = (MessageFlags)driver.ReadByte();
            CreationOrder = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public byte[] HeapId { get; set; }
        public MessageFlags MessageFlags { get; set; }
        public uint CreationOrder { get; set; }

        #endregion
    }
}

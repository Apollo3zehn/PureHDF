namespace PureHDF
{
    internal struct BTree2Record05 : IBTree2Record
    {
        #region Constructors

        public BTree2Record05(H5DriverBase driver)
        {
            NameHash = driver.ReadUInt32();
            HeapId = driver.ReadBytes(7);
        }

        #endregion

        #region Properties

        public uint NameHash { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}

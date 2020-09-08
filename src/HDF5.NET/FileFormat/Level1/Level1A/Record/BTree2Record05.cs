namespace HDF5.NET
{
    public struct BTree2Record05 : IBTree2Record
    {
        #region Constructors

        public BTree2Record05(H5BinaryReader reader)
        {
            this.NameHash = reader.ReadBytes(4);
            this.HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public byte[] NameHash { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}

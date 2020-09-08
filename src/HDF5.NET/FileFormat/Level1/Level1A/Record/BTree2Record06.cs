namespace HDF5.NET
{
    public struct BTree2Record06 : IBTree2Record
    {
        #region Constructors

        public BTree2Record06(H5BinaryReader reader)
        {
            this.CreationOrder = reader.ReadUInt64();
            this.HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public ulong CreationOrder { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}

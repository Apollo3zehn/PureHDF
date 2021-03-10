namespace HDF5.NET
{
    public struct BTree2Record05 : IBTree2Record
    {
        #region Constructors

        public BTree2Record05(H5BinaryReader reader)
        {
            this.NameHash = reader.ReadUInt32();
            this.HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public uint NameHash { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}

namespace HDF5.NET
{
    internal struct BTree2Record05 : IBTree2Record
    {
        #region Constructors

        public BTree2Record05(H5BinaryReader reader)
        {
            NameHash = reader.ReadUInt32();
            HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public uint NameHash { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}

namespace HDF5.NET
{
    internal struct BTree2Record08 : IBTree2Record
    {
        #region Constructors

        public BTree2Record08(H5BinaryReader reader)
        {
            this.HeapId = reader.ReadBytes(8);
            this.MessageFlags = (MessageFlags)reader.ReadByte();
            this.CreationOrder = reader.ReadUInt32();
            this.NameHash = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public byte[] HeapId { get; set; }
        public MessageFlags MessageFlags { get; set; }
        public uint CreationOrder { get; set; }
        public uint NameHash { get; set; }

        #endregion
    }
}

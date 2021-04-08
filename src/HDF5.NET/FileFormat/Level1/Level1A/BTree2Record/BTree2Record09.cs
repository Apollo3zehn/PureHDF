namespace HDF5.NET
{
    internal struct BTree2Record09 : IBTree2Record
    {
        #region Constructors

        public BTree2Record09(H5BinaryReader reader)
        {
            this.HeapId = reader.ReadBytes(8);
            this.MessageFlags = (MessageFlags)reader.ReadByte();
            this.CreationOrder = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public byte[] HeapId { get; set; }
        public MessageFlags MessageFlags { get; set; }
        public uint CreationOrder { get; set; }

        #endregion
    }
}

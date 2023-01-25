namespace PureHDF
{
    internal struct BTree2Record09 : IBTree2Record
    {
        #region Constructors

        public BTree2Record09(H5BaseReader reader)
        {
            HeapId = reader.ReadBytes(8);
            MessageFlags = (MessageFlags)reader.ReadByte();
            CreationOrder = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public byte[] HeapId { get; set; }
        public MessageFlags MessageFlags { get; set; }
        public uint CreationOrder { get; set; }

        #endregion
    }
}

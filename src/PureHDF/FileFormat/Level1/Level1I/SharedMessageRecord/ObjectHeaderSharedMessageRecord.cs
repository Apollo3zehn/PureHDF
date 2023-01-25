namespace PureHDF
{
    internal class ObjectHeaderSharedMessageRecord : SharedMessageRecord
    {
        #region Constructors

        public ObjectHeaderSharedMessageRecord(H5Context context) : base(context.Reader)
        {
            var (reader, superblock) = context;

            // hash value
            HashValue = reader.ReadUInt32();

            // reserved
            reader.ReadByte();

            // message type
            MessageType = (MessageType)reader.ReadByte();

            // creation index
            CreationIndex = reader.ReadUInt16();

            // object header address
            ObjectHeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public uint HashValue { get; set; }
        public MessageType MessageType { get; set; }
        public ushort CreationIndex { get; set; }
        public ulong ObjectHeaderAddress { get; set; }

        #endregion
    }
}
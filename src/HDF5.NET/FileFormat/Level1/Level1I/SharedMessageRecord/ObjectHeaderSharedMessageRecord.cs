namespace HDF5.NET
{
    internal class ObjectHeaderSharedMessageRecord : SharedMessageRecord
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public ObjectHeaderSharedMessageRecord(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // hash value
            this.HashValue = reader.ReadUInt32();

            // reserved
            reader.ReadByte();

            // message type
            this.MessageType = (HeaderMessageType)reader.ReadByte();

            // creation index
            this.CreationIndex = reader.ReadUInt16();

            // object header address
            this.ObjectHeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public uint HashValue { get; set; }
        public HeaderMessageType MessageType { get; set; }
        public ushort CreationIndex { get; set; }
        public ulong ObjectHeaderAddress { get; set; }

        #endregion
    }
}
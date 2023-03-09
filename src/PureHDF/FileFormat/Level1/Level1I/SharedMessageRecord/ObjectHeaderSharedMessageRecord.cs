namespace PureHDF
{
    internal class ObjectHeaderSharedMessageRecord : SharedMessageRecord
    {
        #region Constructors

        public ObjectHeaderSharedMessageRecord(H5Context context) : base(context.Driver)
        {
            var (driver, superblock) = context;

            // hash value
            HashValue = driver.ReadUInt32();

            // reserved
            driver.ReadByte();

            // message type
            MessageType = (MessageType)driver.ReadByte();

            // creation index
            CreationIndex = driver.ReadUInt16();

            // object header address
            ObjectHeaderAddress = superblock.ReadOffset(driver);
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
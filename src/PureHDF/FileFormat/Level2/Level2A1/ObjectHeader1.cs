namespace PureHDF
{
    internal class ObjectHeader1 : ObjectHeader
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        internal ObjectHeader1(H5Context context, byte version) : base(context)
        {
            // version
            Version = version;

            // reserved
            context.Reader.ReadByte();

            // header messages count
            HeaderMessagesCount = context.Reader.ReadUInt16();

            // object reference count
            ObjectReferenceCount = context.Reader.ReadUInt32();

            // object header size
            ObjectHeaderSize = context.Reader.ReadUInt32();

            // header messages

            // read padding bytes that align the following message to an 8 byte boundary
            if (ObjectHeaderSize > 0)
                context.Reader.ReadBytes(4);

            var messages = ReadHeaderMessages(context, ObjectHeaderSize, 1);
            HeaderMessages.AddRange(messages);
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(ObjectHeader1)} are supported.");

                _version = value;
            }
        }

        public ushort HeaderMessagesCount { get; set; }
        public uint ObjectReferenceCount { get; set; }
        public uint ObjectHeaderSize { get; set; }

        #endregion
    }
}

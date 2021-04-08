using System;

namespace HDF5.NET
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
            this.Version = version;

            // reserved
            context.Reader.ReadByte();

            // header messages count
            this.HeaderMessagesCount = context.Reader.ReadUInt16();

            // object reference count
            this.ObjectReferenceCount = context.Reader.ReadUInt32();

            // object header size
            this.ObjectHeaderSize = context.Reader.ReadUInt32();

            // header messages

            // read padding bytes that align the following message to an 8 byte boundary
            if (this.ObjectHeaderSize > 0)
                context.Reader.ReadBytes(4);

            var messages = this.ReadHeaderMessages(context, this.ObjectHeaderSize, 1);
            this.HeaderMessages.AddRange(messages);
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

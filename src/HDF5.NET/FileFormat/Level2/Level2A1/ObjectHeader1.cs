using System;
using System.IO;

namespace HDF5.NET
{
    public class ObjectHeader1 : ObjectHeader
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public ObjectHeader1(BinaryReader reader, Superblock superblock, byte version) : base(reader)
        {
            // version
            this.Version = version;

            // reserved
            reader.ReadByte();

            // header messages count
            this.HeaderMessagesCount = reader.ReadUInt16();

            // object reference count
            this.ObjectReferenceCount = reader.ReadUInt32();

            // object header size
            this.ObjectHeaderSize = reader.ReadUInt32();

            // header messages

            // read padding bytes that align the following message to an 8 byte boundary
            if (this.ObjectHeaderSize > 0)
                reader.ReadBytes(4);

            var messages = this.ReadHeaderMessages(reader, superblock, this.ObjectHeaderSize, 1);
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

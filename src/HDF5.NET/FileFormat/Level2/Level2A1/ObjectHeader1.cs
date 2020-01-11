using System;
using System.Collections.Generic;
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
            this.HeaderMessages = new List<HeaderMessage>();

            // read padding bytes that align the following message to an 8 byte boundary
            var remainingBytes = this.ObjectHeaderSize;

            if (remainingBytes > 0)
                reader.ReadBytes(4);

            while (remainingBytes > 0)
            {
                var message = new HeaderMessage(reader, superblock, 1);
                remainingBytes -= (uint)message.DataSize + 2 + 2 + 1 + 3;
                this.HeaderMessages.Add(message);
            }
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

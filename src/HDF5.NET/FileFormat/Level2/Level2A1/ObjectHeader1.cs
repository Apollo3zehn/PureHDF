using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            var messages = this.ReadHeaderMessages(reader, superblock, this.ObjectHeaderSize);
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

        #region Methods

        private List<HeaderMessage> ReadHeaderMessages(BinaryReader reader, Superblock superblock, ulong objectHeaderSize)
        {
            var headerMessages = new List<HeaderMessage>();
            var continuationMessages = new List<ObjectHeaderContinuationMessage>();
            var remainingBytes = objectHeaderSize;

            while (remainingBytes > 0)
            {
                var message = new HeaderMessage(reader, superblock, 1);
                remainingBytes -= (uint)message.DataSize + 2 + 2 + 1 + 3;

                if (message.Type == HeaderMessageType.ObjectHeaderContinuation)
                {
                    continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);
                }
                else
                {
                    headerMessages.Add(message);

                    if (message.Type == HeaderMessageType.SymbolTable)
                        this.ObjectType = H5ObjectType.Group;

                    else if (message.Type == HeaderMessageType.DataLayout)
                        this.ObjectType = H5ObjectType.Dataset;
                }
            }

            foreach (var continuationMessage in continuationMessages)
            {
                this.Reader.BaseStream.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);
                var messages = this.ReadHeaderMessages(reader, superblock, continuationMessage.Length);
                headerMessages.AddRange(messages);
            }

            var condition = this.ObjectType == H5ObjectType.Undefined
                            && headerMessages.Count == 1
                            && headerMessages[0].Type == HeaderMessageType.DataType;

            if (condition)
                this.ObjectType = H5ObjectType.CommitedDataType;

            return headerMessages;
        }

        #endregion
    }
}

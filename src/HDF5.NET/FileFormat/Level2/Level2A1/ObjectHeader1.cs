using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class ObjectHeader1 : ObjectHeader
    {
        #region Constructors

        public ObjectHeader1(byte version, BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Version = version;
            reader.ReadByte();
            this.HeaderMessagesCount = reader.ReadUInt16();

            this.ObjectReferenceCount = reader.ReadUInt32();
            this.ObjectHeaderSize = reader.ReadUInt32();

            // read header messages
            var remainingBytes = this.ObjectHeaderSize;

            this.HeaderMessages = new List<HeaderMessage>();

            // read padding bytes that align the following messages to an 8 byte boundary
            if (remainingBytes > 0)
                reader.ReadBytes(4);

            while (remainingBytes > 0)
            {
                var message = new HeaderMessage(reader, superblock);
                remainingBytes -= (uint)message.TotalMessageSize;

                this.HeaderMessages.Add(message);
            }
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ushort HeaderMessagesCount { get; set; }
        public uint ObjectReferenceCount { get; set; }
        public uint ObjectHeaderSize { get; set; }

        #endregion
    }
}

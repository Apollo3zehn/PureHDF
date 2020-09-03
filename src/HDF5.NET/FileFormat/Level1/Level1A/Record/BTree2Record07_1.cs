using System.IO;

namespace HDF5.NET
{
    public class BTree2Record07_1 : BTree2Record07
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public BTree2Record07_1(BinaryReader reader, Superblock superblock, MessageLocation messageLocation) 
            : base(reader, messageLocation)
        {
            _superblock = superblock;

            // hash
            this.Hash = reader.ReadBytes(4);

            // reserved
            reader.ReadByte();

            // message type
            this.MessageType = (HeaderMessageType)reader.ReadByte();

            // object header index
            this.ObjectHeaderIndex = reader.ReadUInt16();

            // object header address
            this.ObjectHeaderAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public byte[] Hash { get; set; }
        public HeaderMessageType MessageType { get; set; }
        public ushort ObjectHeaderIndex { get; set; }
        public ulong ObjectHeaderAddress { get; set; }

        public ObjectHeader ObjectHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.ObjectHeaderAddress, SeekOrigin.Begin);
                return ObjectHeader.Construct(this.Reader, _superblock);
            }
        }

        #endregion
    }
}

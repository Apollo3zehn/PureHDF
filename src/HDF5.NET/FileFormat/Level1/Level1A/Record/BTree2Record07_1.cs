using System.IO;

namespace HDF5.NET
{
    public class BTree2Record07_1 : BTree2Record
    {
        #region Fields

#warning OK like this?
        private Superblock _superblock;

        #endregion

        #region Constructors

        public BTree2Record07_1(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // message location
            this.MessageLocation = (MessageLocation)reader.ReadByte();

            // hash
            this.Hash = reader.ReadBytes(4);

            // reserved
            reader.ReadByte();

            // message type
            //this.MessageType = (MessageType)reader.ReadByte();

            // object header index
            this.ObjectHeaderIndex = reader.ReadUInt16();

            // object header address
            this.ObjectHeaderAddress = superblock.ReadOffset();
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public byte[] Hash { get; set; }
        //public MessageType MessageType { get; set; }
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

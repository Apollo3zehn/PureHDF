//using System.IO;

//namespace HDF5.NET
//{
//    public class BTree2Record07_1 : BTree2Record07
//    {
//        #region Fields

//        private H5BinaryReader _reader;
//        private Superblock _superblock;

//        #endregion

//        #region Constructors

//        public BTree2Record07_1(H5BinaryReader reader, Superblock superblock, MessageLocation messageLocation) 
//            : base(messageLocation)
//        {
//            _reader = reader;
//            _superblock = superblock;

//            // hash
//            this.Hash = reader.ReadBytes(4);

//            // reserved
//            reader.ReadByte();

//            // message type
//            this.MessageType = (HeaderMessageType)reader.ReadByte();

//            // object header index
//            this.ObjectHeaderIndex = reader.ReadUInt16();

//            // object header address
//            this.ObjectHeaderAddress = superblock.ReadOffset(reader);
//        }

//        #endregion

//        #region Properties

//        public byte[] Hash { get; set; }
//        public HeaderMessageType MessageType { get; set; }
//        public ushort ObjectHeaderIndex { get; set; }
//        public ulong ObjectHeaderAddress { get; set; }

//        public ObjectHeader ObjectHeader
//        {
//            get
//            {
//                _reader.Seek((long)this.ObjectHeaderAddress, SeekOrigin.Begin);
//                return ObjectHeader.Construct(_reader, _superblock);
//            }
//        }

//        #endregion
//    }
//}

//using System.IO;

//namespace HDF5.NET
//{
//    internal class BTree2Record07_1 : BTree2Record07
//    {
//        #region Fields

//        private H5BinaryReader _reader;
//        private Superblock _superblock;

//        #endregion

//        #region Constructors

//        public BTree2Record07_1(H5BinaryReader reader, MessageLocation messageLocation) 
//            : base(messageLocation)
//        {
//            // hash
//            Hash = reader.ReadBytes(4);

//            // reserved
//            reader.ReadByte();

//            // message type
//            MessageType = (HeaderMessageType)reader.ReadByte();

//            // object header index
//            HeaderIndex = reader.ReadUInt16();

//            // object header address
//            HeaderAddress = superblock.ReadOffset(reader);
//        }

//        #endregion

//        #region Properties

//        public byte[] Hash { get; set; }
//        public HeaderMessageType MessageType { get; set; }
//        public ushort HeaderIndex { get; set; }
//        public ulong HeaderAddress { get; set; }

//        #endregion
//    }
//}

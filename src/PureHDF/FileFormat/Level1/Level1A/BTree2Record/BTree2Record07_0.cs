//namespace PureHDF
//{
//    internal class BTree2Record07_0 : BTree2Record07
//    {
//        #region Constructors

//        internal BTree2Record07_0(H5BinaryReader reader, MessageLocation messageLocation)
//            : base(messageLocation)
//        {
//            Hash = reader.ReadBytes(4);
//            ReferenceCount = reader.ReadUInt32();
//            HeapId = reader.ReadBytes(8);
//        }

//        #endregion

//        #region Properties

//        public byte[] Hash { get; set; }
//        public uint ReferenceCount { get; set; }
//        public byte[] HeapId { get; set; }

//        #endregion
//    }
//}

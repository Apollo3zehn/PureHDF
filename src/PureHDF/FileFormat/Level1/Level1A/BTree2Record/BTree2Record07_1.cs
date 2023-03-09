//using System.IO;

//namespace PureHDF
//{
//    internal class BTree2Record07_1 : BTree2Record07
//    {
//        #region Fields

//        private H5DriverBase _driver;
//        private Superblock _superblock;

//        #endregion

//        #region Constructors

//        public BTree2Record07_1(H5DriverBase driver, MessageLocation messageLocation) 
//            : base(messageLocation)
//        {
//            // hash
//            Hash = driver.ReadBytes(4);

//            // reserved
//            driver.ReadByte();

//            // message type
//            MessageType = (HeaderMessageType)driver.ReadByte();

//            // object header index
//            HeaderIndex = driver.ReadUInt16();

//            // object header address
//            HeaderAddress = superblock.ReadOffset(driver);
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

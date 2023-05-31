// namespace PureHDF.VOL.Native;
//
// internal class BTree2Record07_0 : BTree2Record07
// {
//     #region Constructors

//     internal BTree2Record07_0(H5DriverBase driver, MessageLocation messageLocation)
//         : base(messageLocation)
//     {
//         Hash = driver.ReadBytes(4);
//         ReferenceCount = driver.ReadUInt32();
//         HeapId = driver.ReadBytes(8);
//     }

//     #endregion

//     #region Properties

//     public byte[] Hash { get; set; }
//     public uint ReferenceCount { get; set; }
//     public byte[] HeapId { get; set; }

//     #endregion
// }
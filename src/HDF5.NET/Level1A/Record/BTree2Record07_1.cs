namespace HDF5.NET
{
    public class BTree2Record07_1
    {
        #region Constructors

        public BTree2Record07_1()
        {
            //
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public uint Hash { get; set; }
        public byte Reserved { get; set; }
        public MessageType MessageType { get; set; }
        public ushort ObjectHeaderIndex { get; set; }
        public ulong ObjectHeaderAddress { get; set; }

        #endregion
    }
}

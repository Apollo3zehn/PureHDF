namespace HDF5.NET
{
    public class BTree2Header
    {
        #region Constructors

        public BTree2Header()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public BTree2Type Type { get; set; }
        public uint NodeSize { get; set; }
        public ushort RecordSize { get; set; }
        public ushort Depth { get; set; }
        public byte SplitPercent { get; set; }
        public byte MergePercent { get; set; }
        public ulong RootNodeAddress { get; set; }
        public ushort RecordCountInRootNode { get; set; }
        public ulong RecordCountInBTree { get; set; }
        public uint Checksum { get; set; }


        #endregion
    }
}

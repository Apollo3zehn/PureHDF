namespace HDF5.NET
{
    internal struct BTree2NodeInfo
    {
        public uint MaxRecordCount { get; set; }
        public uint SplitRecordCount { get; set; }
        public uint MergeRecordCount { get; set; }
        public uint CumulatedTotalRecordCount { get; set; }
        public byte CumulatedTotalRecordCountSize { get; set; }
    }
}

namespace HDF5.NET
{
    public struct BTree2NodePointer
    {
        public ulong Address { get; set; }
        public ushort RecordCount { get; set; }
        public ulong TotalRecordCount { get; set; }
    }
}

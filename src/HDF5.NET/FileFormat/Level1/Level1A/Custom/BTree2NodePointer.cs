namespace HDF5.NET
{
    internal struct BTree2NodePointer
    {
        public ulong Address { get; set; }
        public ushort RecordCount { get; set; }
        public ulong TotalRecordCount { get; set; }
    }
}

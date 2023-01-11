namespace HDF5.NET
{
    internal struct BTree2Record04 : IBTree2Record
    {
        #region Constructors

        public BTree2Record04(H5Context context)
        {
            var (reader, superblock) = context;
            
            FilteredHugeObjectAddress = superblock.ReadOffset(reader);
            FilteredHugeObjectLength = superblock.ReadLength(reader);
            FilterMask = reader.ReadUInt32();
            FilteredHugeObjectMemorySize = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong FilteredHugeObjectAddress { get; set; }
        public ulong FilteredHugeObjectLength { get; set; }
        public uint FilterMask { get; set; }
        public ulong FilteredHugeObjectMemorySize { get; set; }

        #endregion
    }
}

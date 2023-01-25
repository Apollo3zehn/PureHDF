namespace HDF5.NET
{
    internal struct BTree2Record02 : IBTree2Record
    {
        #region Constructors

        public BTree2Record02(H5Context context)
        {
            var (reader, superblock) = context;

            FilteredHugeObjectAddress = superblock.ReadOffset(reader);
            FilteredHugeObjectLength = superblock.ReadLength(reader);
            FilterMask = reader.ReadUInt32();
            FilteredHugeObjectMemorySize = superblock.ReadLength(reader);
            HugeObjectId = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong FilteredHugeObjectAddress { get; set; }
        public ulong FilteredHugeObjectLength { get; set; }
        public uint FilterMask { get; set; }
        public ulong FilteredHugeObjectMemorySize { get; set; }
        public ulong HugeObjectId { get; set; }

        #endregion
    }
}

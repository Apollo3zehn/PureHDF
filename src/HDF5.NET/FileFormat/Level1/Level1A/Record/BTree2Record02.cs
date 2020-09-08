namespace HDF5.NET
{
    public struct BTree2Record02 : IBTree2Record
    {
        #region Constructors

        public BTree2Record02(H5BinaryReader reader, Superblock superblock)
        {
            this.FilteredHugeObjectAddress = superblock.ReadOffset(reader);
            this.FilteredHugeObjectLength = superblock.ReadLength(reader);
            this.FilterMask = reader.ReadUInt32();
            this.FilteredHugeObjectMemorySize = superblock.ReadLength(reader);
            this.HugeObjectId = superblock.ReadLength(reader);
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

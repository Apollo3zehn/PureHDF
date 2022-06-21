namespace HDF5.NET
{
    internal struct BTree2Record01 : IBTree2Record
    {
        #region Constructors

        public BTree2Record01(H5BinaryReader reader, Superblock superblock)
        {
            HugeObjectAddress = superblock.ReadOffset(reader);
            HugeObjectLength = superblock.ReadLength(reader);
            HugeObjectId = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }
        public ulong HugeObjectId { get; set; }

        #endregion
    }
}

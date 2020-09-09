namespace HDF5.NET
{
    public struct BTree2Record01 : IBTree2Record
    {
        #region Constructors

        public BTree2Record01(H5BinaryReader reader, Superblock superblock)
        {
            this.HugeObjectAddress = superblock.ReadOffset(reader);
            this.HugeObjectLength = superblock.ReadLength(reader);
            this.HugeObjectId = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }
        public ulong HugeObjectId { get; set; }

        #endregion
    }
}

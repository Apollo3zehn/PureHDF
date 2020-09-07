namespace HDF5.NET
{
    public class BTree2Record03 : BTree2Record
    {
        #region Constructors

        public BTree2Record03(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.HugeObjectAddress = superblock.ReadOffset(reader);
            this.HugeObjectLength = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }

        #endregion
    }
}

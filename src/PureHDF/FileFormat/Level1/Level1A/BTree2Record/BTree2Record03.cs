namespace PureHDF
{
    internal struct BTree2Record03 : IBTree2Record
    {
        #region Constructors

        public BTree2Record03(H5Context context)
        {
            var (driver, superblock) = context;

            HugeObjectAddress = superblock.ReadOffset(driver);
            HugeObjectLength = superblock.ReadLength(driver);
        }

        #endregion

        #region Properties

        public ulong HugeObjectAddress { get; set; }
        public ulong HugeObjectLength { get; set; }

        #endregion
    }
}

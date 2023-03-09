namespace PureHDF
{
    internal struct BTree1GroupKey : IBTree1Key
    {
        #region Constructors

        public BTree1GroupKey(H5Context context)
        {
            var (driver, superblock) = context;
            LocalHeapByteOffset = superblock.ReadLength(driver);
        }

        #endregion

        #region Properties

        public ulong LocalHeapByteOffset { get; set; }

        #endregion
    }
}

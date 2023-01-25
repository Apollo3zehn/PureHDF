namespace PureHDF
{
    internal struct BTree1GroupKey : IBTree1Key
    {
        #region Constructors

        public BTree1GroupKey(H5Context context)
        {
            var (reader, superblock) = context;
            LocalHeapByteOffset = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong LocalHeapByteOffset { get; set; }

        #endregion
    }
}

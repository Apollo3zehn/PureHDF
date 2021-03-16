namespace HDF5.NET
{
    internal struct BTree1GroupKey : IBTree1Key
    {
        #region Constructors

        public BTree1GroupKey(H5BinaryReader reader, Superblock superblock)
        {
            this.LocalHeapByteOffset = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong LocalHeapByteOffset { get; set; }

        #endregion
    }
}

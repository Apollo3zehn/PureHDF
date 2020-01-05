namespace HDF5.NET
{
    public abstract class BTree1RawDataChunksKey : BTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey()
        {
            //
        }

        #endregion

        #region Properties

        public uint ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public ulong[] ChunkOffset { get; set; }

        #endregion
    }
}

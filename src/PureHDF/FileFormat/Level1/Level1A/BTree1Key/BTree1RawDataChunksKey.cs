namespace PureHDF
{
    internal struct BTree1RawDataChunksKey : IBTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey(H5DriverBase driver, byte rank, ulong[] rawChunkDims)
        {
            // H5Dbtree.c (H5D__btree_decode_key)

            ChunkSize = driver.ReadUInt32();
            FilterMask = driver.ReadUInt32();

            ScaledChunkOffsets = new ulong[rank + 1];

            for (byte i = 0; i < rank + 1; i++) // Do not change this! We MUST read rank + 1 values!
            {
                ScaledChunkOffsets[i] = driver.ReadUInt64() / rawChunkDims[i];
            }
        }

        #endregion

        #region Properties

        public uint ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public ulong[] ScaledChunkOffsets { get; set; }

        #endregion
    }
}

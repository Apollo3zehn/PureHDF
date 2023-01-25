namespace PureHDF
{
    internal struct BTree1RawDataChunksKey : IBTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey(H5BaseReader reader, byte rank, ulong[] rawChunkDims)
        {
            // H5Dbtree.c (H5D__btree_decode_key)

            ChunkSize = reader.ReadUInt32();
            FilterMask = reader.ReadUInt32();

            ScaledChunkOffsets = new ulong[rank + 1];

            for (byte i = 0; i < rank + 1; i++) // Do not change this! We MUST read rank + 1 values!
            {
                ScaledChunkOffsets[i] = reader.ReadUInt64() / rawChunkDims[i];
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

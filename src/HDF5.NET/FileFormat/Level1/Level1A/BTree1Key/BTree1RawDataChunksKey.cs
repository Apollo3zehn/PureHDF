namespace HDF5.NET
{
    public struct BTree1RawDataChunksKey : IBTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey(H5BinaryReader reader, byte rank, uint[] dimensionSizes)
        {
            // H5Dbtree.c (H5D__btree_decode_key)

            this.ChunkSize = reader.ReadUInt32();
            this.FilterMask = reader.ReadUInt32();

            this.ScaledChunkOffsets = new ulong[rank + 1];

            for (byte i = 0; i < rank + 1; i++)
            {
                this.ScaledChunkOffsets[i] = reader.ReadUInt64() / dimensionSizes[i];
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

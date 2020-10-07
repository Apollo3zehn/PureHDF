namespace HDF5.NET
{
    public struct BTree1RawDataChunksKey : IBTree1Key
    {
        #region Constructors

        public BTree1RawDataChunksKey(H5BinaryReader reader, byte rank)
        {
            this.ChunkSize = reader.ReadUInt32();
            this.FilterMask = reader.ReadUInt32();

            this.ChunkOffsets = new ulong[rank + 1];

            for (byte i = 0; i < rank + 1; i++)
            {
                this.ChunkOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public uint ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public ulong[] ChunkOffsets { get; set; }

        #endregion
    }
}

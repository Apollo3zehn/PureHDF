namespace HDF5.NET
{
    internal struct BTree2Record11 : IBTree2Record
    {
        #region Constructors

        public BTree2Record11(H5BinaryReader reader, Superblock superblock, byte rank, uint chunkSizeLength)
        {
            // address
            Address = superblock.ReadOffset(reader);

            // chunk size
            ChunkSize = H5Utils.ReadUlong(reader, chunkSizeLength);

            // filter mask
            FilterMask = reader.ReadUInt32();

            // scaled offsets
            ScaledOffsets = new ulong[rank];

            for (int i = 0; i < rank; i++)
            {
                ScaledOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public ulong[] ScaledOffsets { get; set; }

        #endregion
    }
}

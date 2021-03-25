namespace HDF5.NET
{
    internal struct BTree2Record10 : IBTree2Record
    {
        #region Constructors

        public BTree2Record10(H5BinaryReader reader, Superblock superblock, byte rank)
        {
            // address
            this.Address = superblock.ReadOffset(reader);

            // scaled offsets
            this.ScaledOffsets = new ulong[rank];

            for (int i = 0; i < rank; i++)
            {
                this.ScaledOffsets[i] = reader.ReadUInt64();
            }
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong[] ScaledOffsets { get; set; }

        #endregion
    }
}

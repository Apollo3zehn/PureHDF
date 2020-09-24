namespace HDF5.NET
{
    public struct BTree2Record10 : IBTree2Record
    {
        #region Constructors

        public BTree2Record10(H5BinaryReader reader, Superblock superblock, byte dimensionality)
        {
            // address
            this.Address = superblock.ReadOffset(reader);

            // scaled offsets
            this.ScaledOffsets = new ulong[dimensionality];

            for (int i = 0; i < dimensionality; i++)
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

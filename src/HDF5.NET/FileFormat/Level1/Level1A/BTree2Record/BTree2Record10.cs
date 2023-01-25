namespace HDF5.NET
{
    internal struct BTree2Record10 : IBTree2Record
    {
        #region Constructors

        public BTree2Record10(H5Context context, byte rank)
        {
            var (reader, superblock) = context;

            // address
            Address = superblock.ReadOffset(reader);

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
        public ulong[] ScaledOffsets { get; set; }

        #endregion
    }
}

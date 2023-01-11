namespace HDF5.NET
{
    internal class ChunkedStoragePropertyDescription3 : ChunkedStoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription3(H5Context context)
        {
            var (reader, superblock) = context;
            
            // rank
            Rank = reader.ReadByte();

            // address
            Address = superblock.ReadOffset(reader);

            // dimension sizes
            DimensionSizes = new uint[Rank];

            for (uint i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = reader.ReadUInt32();
            }
        }

        #endregion

        #region Properties

        public uint[] DimensionSizes { get; set; }

        #endregion
    }
}

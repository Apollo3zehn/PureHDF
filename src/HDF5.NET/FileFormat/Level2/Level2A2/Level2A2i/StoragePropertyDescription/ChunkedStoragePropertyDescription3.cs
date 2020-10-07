namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription3 : ChunkedStoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription3(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // rank
            this.Rank = reader.ReadByte();

            // address
            this.Address = superblock.ReadOffset(reader);

            // dimension sizes
            this.DimensionSizes = new uint[this.Rank];

            for (uint i = 0; i < this.Rank; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }
        }

        #endregion

        #region Properties

        public uint[] DimensionSizes { get; set; }

        #endregion
    }
}

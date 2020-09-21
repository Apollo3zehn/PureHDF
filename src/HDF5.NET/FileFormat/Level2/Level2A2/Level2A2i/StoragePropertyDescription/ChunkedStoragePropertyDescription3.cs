namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription3 : ChunkedStoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription3(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // address
            this.Address = superblock.ReadOffset(reader);

            // dimension sizes
            this.DimensionSizes = new uint[this.Dimensionality];

            for (uint i = 0; i < this.Dimensionality; i++)
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

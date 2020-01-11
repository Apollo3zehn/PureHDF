using System.IO;

namespace HDF5.NET
{
    public class ChunkedStoragePropertyDescription3 : StoragePropertyDescription
    {
        #region Constructors

        public ChunkedStoragePropertyDescription3(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // address
            this.Address = superblock.ReadOffset();

            // dimension sizes
            this.DimensionSizes = new uint[this.Dimensionality];

            for (uint i = 0; i < this.Dimensionality; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }

            // dataset element size
            this.DatasetElementSize = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public ulong Address { get; set; }
        public uint[] DimensionSizes { get; set; }
        public uint DatasetElementSize { get; set; }

        #endregion
    }
}

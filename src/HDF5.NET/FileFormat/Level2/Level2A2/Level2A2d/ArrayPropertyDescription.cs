namespace HDF5.NET
{
    public class ArrayPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription(H5BinaryReader reader, byte version) : base(reader)
        {
            // rank
            this.Rank = reader.ReadByte();

            // reserved
            if (version == 2)
                reader.ReadBytes(3);

            // dimension sizes
            this.DimensionSizes = new uint[this.Rank];

            for (int i = 0; i < this.Rank; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }

            // permutation indices
            this.PermutationIndices = new uint[this.Rank];

            if (version == 2)
            {
                for (int i = 0; i < this.Rank; i++)
                {
                    this.PermutationIndices[i] = reader.ReadUInt32();
                }
            }
                
            // base type
            this.BaseType = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public byte Rank { get; set; }
        public uint[] DimensionSizes { get; set; }
        public uint[] PermutationIndices { get; set; }
        public DatatypeMessage BaseType { get; set; }

        #endregion
    }
}
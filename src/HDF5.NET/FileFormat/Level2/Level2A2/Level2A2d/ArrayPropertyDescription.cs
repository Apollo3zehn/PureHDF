namespace HDF5.NET
{
    internal class ArrayPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription(H5BinaryReader reader, byte version) : base(reader)
        {
            // rank
            Rank = reader.ReadByte();

            // reserved
            if (version == 2)
                reader.ReadBytes(3);

            // dimension sizes
            DimensionSizes = new uint[Rank];

            for (int i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = reader.ReadUInt32();
            }

            // permutation indices
            PermutationIndices = new uint[Rank];

            if (version == 2)
            {
                for (int i = 0; i < Rank; i++)
                {
                    PermutationIndices[i] = reader.ReadUInt32();
                }
            }
                
            // base type
            BaseType = new DatatypeMessage(reader);
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
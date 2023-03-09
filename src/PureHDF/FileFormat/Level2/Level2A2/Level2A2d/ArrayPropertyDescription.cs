namespace PureHDF
{
    internal class ArrayPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public ArrayPropertyDescription(H5DriverBase driver, byte version)
        {
            // rank
            Rank = driver.ReadByte();

            // reserved
            if (version == 2)
                driver.ReadBytes(3);

            // dimension sizes
            DimensionSizes = new uint[Rank];

            for (int i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = driver.ReadUInt32();
            }

            // permutation indices
            PermutationIndices = new uint[Rank];

            if (version == 2)
            {
                for (int i = 0; i < Rank; i++)
                {
                    PermutationIndices[i] = driver.ReadUInt32();
                }
            }

            // base type
            BaseType = new DatatypeMessage(driver);
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
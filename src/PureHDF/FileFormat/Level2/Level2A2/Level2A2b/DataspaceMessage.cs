namespace PureHDF
{
    internal class DataspaceMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DataspaceMessage(H5Context context)
        {
            var (reader, superblock) = context;

            Version = reader.ReadByte();
            Rank = reader.ReadByte();
            var flags = (DataspaceMessageFlags)reader.ReadByte();

            if (Version == 1)
            {
                if (Rank > 0)
                    Type = DataspaceType.Simple;
                else
                    Type = DataspaceType.Scalar;

                reader.ReadBytes(5);
            }
            else
            {
                Type = (DataspaceType)reader.ReadByte();
            }

            DimensionSizes = new ulong[Rank];

            var dimensionMaxSizesArePresent = flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
            var permutationIndicesArePresent = flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

            for (int i = 0; i < Rank; i++)
            {
                DimensionSizes[i] = superblock.ReadLength(reader);
            }

            if (dimensionMaxSizesArePresent)
            {
                DimensionMaxSizes = new ulong[Rank];

                for (int i = 0; i < Rank; i++)
                {
                    DimensionMaxSizes[i] = superblock.ReadLength(reader);
                }
            }
            else
            {
                DimensionMaxSizes = DimensionSizes.ToArray();
            }

            if (permutationIndicesArePresent)
            {
                PermutationIndices = new ulong[Rank];

                for (int i = 0; i < Rank; i++)
                {
                    PermutationIndices[i] = superblock.ReadLength(reader);
                }
            }
        }

        #endregion

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (!(1 <= value && value <= 2))
                    throw new NotSupportedException("The dataspace message version must be in the range of 1..2.");

                _version = value;
            }
        }

        public byte Rank { get; set; }
        public DataspaceType Type { get; set; }
        public ulong[] DimensionSizes { get; set; }
        public ulong[] DimensionMaxSizes { get; set; }
        public ulong[]? PermutationIndices { get; set; }

        #endregion
    }
}

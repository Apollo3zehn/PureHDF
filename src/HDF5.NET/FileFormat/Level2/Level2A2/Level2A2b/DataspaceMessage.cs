using System;
using System.Linq;

namespace HDF5.NET
{
    public class DataspaceMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DataspaceMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Version = reader.ReadByte();
            this.Rank = reader.ReadByte();
            var flags = (DataspaceMessageFlags)reader.ReadByte();

            if (this.Version == 1)
            {
                if (this.Rank > 0)
                    this.Type = DataspaceType.Simple;
                else
                    this.Type = DataspaceType.Scalar;

                reader.ReadBytes(5);
            }
            else
            {
                this.Type = (DataspaceType)reader.ReadByte();
            }

            this.DimensionSizes = new ulong[this.Rank];

            var dimensionMaxSizesArePresent = flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
            var permutationIndicesArePresent = flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

            for (int i = 0; i < this.Rank; i++)
            {
                this.DimensionSizes[i] = superblock.ReadLength(reader);
            }

            if (dimensionMaxSizesArePresent)
            {
                this.DimensionMaxSizes = new ulong[this.Rank];
                
                for (int i = 0; i < this.Rank; i++)
                {
                    this.DimensionMaxSizes[i] = superblock.ReadLength(reader);
                }
            }
            else
            {
                this.DimensionMaxSizes = this.DimensionSizes.ToArray();
            }

            if (permutationIndicesArePresent)
            {
                this.PermutationIndices = new ulong[this.Rank];

                for (int i = 0; i < this.Rank; i++)
                {
                    this.PermutationIndices[i] = superblock.ReadLength(reader);
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

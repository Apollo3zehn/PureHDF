using System;
using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class DataspaceMessage : Message
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public DataspaceMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Version = reader.ReadByte();
            this.Dimensionality = reader.ReadByte();
            this.Flags = (DataspaceMessageFlags)reader.ReadByte();

            if (this.Version == 1)
                reader.ReadBytes(5); 
            else if (this.Version == 2)
                this.Flags = (DataspaceMessageFlags)reader.ReadByte();

            this.DimensionSizes = new List<ulong>();
            this.DimensionMaxSizes = new List<ulong>();
            this.PermutationIndices = new List<ulong>();

            var dimensionMaxSizesArePresent = this.Flags.HasFlag(DataspaceMessageFlags.DimensionMaxSizes);
            var permutationIndicesArePresent = this.Flags.HasFlag(DataspaceMessageFlags.PermuationIndices);

            for (int i = 0; i < this.Dimensionality; i++)
            {
                this.DimensionSizes.Add(superblock.ReadLength(reader));
            }

            if (dimensionMaxSizesArePresent)
            {
                for (int i = 0; i < this.Dimensionality; i++)
                {
                    this.DimensionMaxSizes.Add(superblock.ReadLength(reader));
                }
            }

            if (permutationIndicesArePresent)
            {
                for (int i = 0; i < this.Dimensionality; i++)
                {
                    this.PermutationIndices.Add(superblock.ReadLength(reader));
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

        public byte Dimensionality { get; set; }
        public DataspaceMessageFlags Flags { get; set; }
        public DataspaceMessageType Type { get; set; }
        public List<ulong> DimensionSizes { get; set; }
        public List<ulong> DimensionMaxSizes { get; set; }
        public List<ulong> PermutationIndices { get; set; }

        #endregion
    }
}

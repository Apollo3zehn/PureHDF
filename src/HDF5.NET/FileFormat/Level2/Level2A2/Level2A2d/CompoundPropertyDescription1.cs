using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class CompoundPropertyDescription1 : DatatypePropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription1(BinaryReader reader) : base(reader)
        {
            // name
            this.Name = H5Utils.ReadNullTerminatedString(reader, pad: true);

            // member byte offset
            this.MemberByteOffset = reader.ReadUInt32();

            // dimensionality
            this.Dimensionality = reader.ReadByte();

            // padding bytes
            reader.ReadBytes(3);

            // dimension permutation
            this.DimensionPermutation = reader.ReadByte();

            // padding byte
            reader.ReadByte();

            // dimension sizes
            this.DimensionSizes = new List<uint>(4);

            for (int i = 0; i < 4; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }

            // member type message
            this.MemberTypeMessage = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public uint MemberByteOffset { get; set; }
        public byte Dimensionality { get; set; }
        public uint DimensionPermutation { get; set; }
        public List<uint> DimensionSizes { get; set; }
        public DatatypeMessage MemberTypeMessage { get; set; }

        #endregion
    }
}
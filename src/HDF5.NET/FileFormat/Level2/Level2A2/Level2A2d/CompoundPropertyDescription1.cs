namespace HDF5.NET
{
    public class CompoundPropertyDescription1 : CompoundPropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription1(H5BinaryReader reader) : base(reader)
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
            this.DimensionPermutation = reader.ReadUInt32();

            // padding byte
            reader.ReadBytes(4);

            // dimension sizes
            this.DimensionSizes = new uint[4];

            for (int i = 0; i < 4; i++)
            {
                this.DimensionSizes[i] = reader.ReadUInt32();
            }

            // member type message
            this.MemberTypeMessage = new DatatypeMessage(reader);
        }

        #endregion

        #region Properties

        public byte Dimensionality { get; set; }
        public uint DimensionPermutation { get; set; }
        public uint[] DimensionSizes { get; set; }

        #endregion
    }
}
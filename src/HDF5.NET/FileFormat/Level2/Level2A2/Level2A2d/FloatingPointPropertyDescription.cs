namespace HDF5.NET
{
    internal class FloatingPointPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public FloatingPointPropertyDescription(H5BinaryReader reader)
        {
            BitOffset = reader.ReadUInt16();
            BitPrecision = reader.ReadUInt16();
            ExponentLocation = reader.ReadByte();
            ExponentSize = reader.ReadByte();
            MantissaLocation = reader.ReadByte();
            MantissaSize = reader.ReadByte();
            ExponentBias = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }
        public byte ExponentLocation { get; set; }
        public byte ExponentSize { get; set; }
        public byte MantissaLocation { get; set; }
        public byte MantissaSize { get; set; }
        public uint ExponentBias { get; set; }

        #endregion
    }
}
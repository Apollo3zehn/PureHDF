namespace HDF5.NET
{
    internal class BitFieldPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public BitFieldPropertyDescription(H5BinaryReader reader)
        {
            BitOffset = reader.ReadUInt16();
            BitPrecision = reader.ReadUInt16();
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }

        #endregion
    }
}
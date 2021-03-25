namespace HDF5.NET
{
    internal class FixedPointPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public FixedPointPropertyDescription(H5BinaryReader reader) : base(reader)
        {
            this.BitOffset = reader.ReadUInt16();
            this.BitPrecision = reader.ReadUInt16();
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }

        #endregion
    }
}

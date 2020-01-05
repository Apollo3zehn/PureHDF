namespace HDF5.NET
{
    public class FloatingPointPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public FloatingPointPropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }
        public byte ExponenLocation { get; set; }
        public byte ExponenSize { get; set; }
        public byte MantissaLocation { get; set; }
        public byte MantissaSize { get; set; }
        public uint ExponentBias { get; set; }

        #endregion
    }
}
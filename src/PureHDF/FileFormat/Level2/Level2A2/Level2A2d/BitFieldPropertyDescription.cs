namespace PureHDF
{
    internal class BitFieldPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public BitFieldPropertyDescription(H5DriverBase driver)
        {
            BitOffset = driver.ReadUInt16();
            BitPrecision = driver.ReadUInt16();
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }

        #endregion
    }
}
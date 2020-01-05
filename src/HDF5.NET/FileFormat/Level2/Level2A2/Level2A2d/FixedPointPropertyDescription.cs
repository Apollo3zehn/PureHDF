namespace HDF5.NET
{
    public class FixedPointPropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public FixedPointPropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ushort BitOffset { get; set; }
        public ushort BitPrecision { get; set; }

        #endregion
    }
}

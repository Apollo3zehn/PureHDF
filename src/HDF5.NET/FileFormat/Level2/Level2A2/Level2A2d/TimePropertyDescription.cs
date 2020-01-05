namespace HDF5.NET
{
    public class TimePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public TimePropertyDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ushort BitPrecision { get; set; }

        #endregion
    }
}
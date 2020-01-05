namespace HDF5.NET
{
    public class TimeBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public TimeBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder { get; set; }

        #endregion
    }
}

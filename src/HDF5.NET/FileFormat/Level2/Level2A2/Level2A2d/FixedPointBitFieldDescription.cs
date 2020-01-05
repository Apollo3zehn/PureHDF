namespace HDF5.NET
{
    public class FixedPointBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public FixedPointBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder { get; set; }
        public byte PaddingType { get; set; }
        public bool IsSigned { get; set; }

        #endregion
    }
}

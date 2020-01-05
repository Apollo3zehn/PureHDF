namespace HDF5.NET
{
    public class FloatingPointBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public FloatingPointBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder { get; set; }
        public byte PaddingType { get; set; }
        public MantissaNormalization MantissaNormalization { get; set; }
        public byte SignLocation { get; set; }

        #endregion
    }
}

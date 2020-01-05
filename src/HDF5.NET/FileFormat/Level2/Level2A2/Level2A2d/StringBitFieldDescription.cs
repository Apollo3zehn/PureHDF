namespace HDF5.NET
{
    public class StringBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public StringBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public PaddingType PaddingType { get; set; }
        public CharacterSetEncoding CharacterSet { get; set; }

        #endregion
    }
}

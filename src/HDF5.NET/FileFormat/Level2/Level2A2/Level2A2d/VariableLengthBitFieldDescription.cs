namespace HDF5.NET
{
    public class VariableLengthBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public VariableLengthBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public VariableLengthType Type { get; set; }
        public PaddingType PaddingType { get; set; }
        public CharacterSetEncoding CharacterSet { get; set; }

        #endregion
    }
}

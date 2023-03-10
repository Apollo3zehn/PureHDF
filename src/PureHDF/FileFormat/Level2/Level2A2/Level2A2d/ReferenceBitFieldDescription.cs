namespace PureHDF
{
    internal class ReferenceBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public ReferenceBitFieldDescription(H5DriverBase driver) : base(driver)
        {
            //
        }

        #endregion

        #region Properties

        public InternalReferenceType Type
        {
            get
            {
                return (InternalReferenceType)(Data[0] & 0x0F);
            }
            set
            {
                Data[0] &= 0xF0;           // clear bits 0-3
                Data[0] |= (byte)value;    // set bits 0-3, depending on the value
            }
        }

        #endregion
    }
}


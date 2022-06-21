namespace HDF5.NET
{
    internal class ReferenceBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public ReferenceBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ReferenceType Type
        {
            get
            {
                return (ReferenceType)(Data[0] & 0x0F);
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


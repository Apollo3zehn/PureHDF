namespace HDF5.NET
{
    public class ReferenceBitFieldDescription : DatatypeBitFieldDescription
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
                return (ReferenceType)(this.Data[0] & 0x0F);
            }
            set
            {
                this.Data[0] &= 0xF0;           // clear bits 0-3
                this.Data[0] |= (byte)value;    // set bits 0-3, depending on the value
            }
        }

        #endregion
    }
}


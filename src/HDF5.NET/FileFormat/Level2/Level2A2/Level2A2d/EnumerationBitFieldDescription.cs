namespace HDF5.NET
{
    public class EnumerationBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public EnumerationBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ushort MemberCount
        {
            get
            {
                return (ushort)(this.Data[0] + this.Data[1] << 8);
            }
            set
            {
                this.Data[0] = (byte)(value & 0x00FF);
                this.Data[1] = (byte)((value & 0xFF00) >> 8);
            }
        }

        #endregion
    }
}

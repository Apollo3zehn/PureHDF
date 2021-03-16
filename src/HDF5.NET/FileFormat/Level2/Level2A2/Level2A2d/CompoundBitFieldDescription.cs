namespace HDF5.NET
{
    internal class CompoundBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public CompoundBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ushort MemberCount
        {
            get
            {
                return (ushort)(this.Data[0] + (this.Data[1] << 8)); 
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

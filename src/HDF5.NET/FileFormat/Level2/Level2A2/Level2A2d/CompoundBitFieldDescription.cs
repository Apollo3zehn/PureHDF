namespace HDF5.NET
{
    public class CompoundBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public CompoundBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ushort MemberCount { get; set; }

        #endregion
    }
}

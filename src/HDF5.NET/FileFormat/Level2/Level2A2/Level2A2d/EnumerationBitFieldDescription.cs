namespace HDF5.NET
{
    public class EnumerationBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public EnumerationBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ushort MemberCount { get; set; }

        #endregion
    }
}

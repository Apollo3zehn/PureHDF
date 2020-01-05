namespace HDF5.NET
{
    public class ReferenceBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public ReferenceBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ReferenceType Type { get; set; }

        #endregion
    }
}


namespace HDF5.NET
{
    public class OpaqueBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public OpaqueBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public byte AsciiTagByteLength { get; set; }

        #endregion
    }
}

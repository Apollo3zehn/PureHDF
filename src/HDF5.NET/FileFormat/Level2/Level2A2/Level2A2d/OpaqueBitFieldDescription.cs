namespace HDF5.NET
{
    internal class OpaqueBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public OpaqueBitFieldDescription(H5BaseReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte AsciiTagByteLength
        {
            get { return Data[0]; }
            set { Data[0] = value; }
        }

        #endregion
    }
}

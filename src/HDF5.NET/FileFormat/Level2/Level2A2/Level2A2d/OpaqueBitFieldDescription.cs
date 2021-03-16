namespace HDF5.NET
{
    internal class OpaqueBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public OpaqueBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte AsciiTagByteLength
        {
            get { return this.Data[0]; }
            set { this.Data[0] = value; }
        }

        #endregion
    }
}

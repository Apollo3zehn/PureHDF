namespace HDF5.NET
{
    public class H5Dataset : H5Link
    {
        #region Constructors

        internal H5Dataset(string name, ObjectHeader header) : base(name, header)
        {
            //
        }

        #endregion

        #region Properties

        public DatatypeMessage DataType => this.ObjectHeader.GetHeaderMessage<DatatypeMessage>();

        public DataspaceMessage DataSpace => this.ObjectHeader.GetHeaderMessage<DataspaceMessage>();

        #endregion
    }
}

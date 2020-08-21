namespace HDF5.NET
{
    public class H5Dataset : H5Link
    {
        #region Constructors

        internal H5Dataset(string name, ObjectHeader header) : base(name, header)
        {
            this.DataType = header.HeaderMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        public DatatypeMessage DataType { get; }

        #endregion
    }
}

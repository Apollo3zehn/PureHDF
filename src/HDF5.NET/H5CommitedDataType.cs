namespace HDF5.NET
{
    public class H5CommitedDataType : H5Link
    {
        #region Constructors

        internal H5CommitedDataType(string name, ObjectHeader header, Superblock superblock) 
            : base(name, header, superblock)
        {
            this.DataType = header.GetHeaderMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        public DatatypeMessage DataType { get; }

        #endregion
    }
}

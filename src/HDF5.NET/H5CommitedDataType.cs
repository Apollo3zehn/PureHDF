namespace HDF5.NET
{
    public class H5CommitedDataType : H5Link
    {
        #region Constructors

        internal H5CommitedDataType(NamedObject namedObject, Superblock superblock) 
            : base(namedObject, superblock)
        {
            this.DataType = namedObject.Header.GetMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        public DatatypeMessage DataType { get; }

        #endregion
    }
}

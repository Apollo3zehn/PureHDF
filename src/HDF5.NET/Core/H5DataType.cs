namespace HDF5.NET
{
    partial class H5DataType
    {
        #region Fields

        private DatatypeMessage _dataType;

        #endregion

        #region Constructors

        internal H5DataType(DatatypeMessage datatype)
        {
            _dataType = datatype;
        }

        #endregion
    }
}

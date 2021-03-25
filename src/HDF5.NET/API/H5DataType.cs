namespace HDF5.NET
{
    public class H5DataType
    {
        #region Fields

        private DatatypeMessage _dataType;

        #endregion

        #region Properties

        public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

        public uint Size => _dataType.Size;

        #endregion

        #region Constructors

        internal H5DataType(DatatypeMessage datatype)
        {
            _dataType = datatype;
        }

        #endregion
    }
}

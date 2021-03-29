namespace HDF5.NET
{
    public partial class H5DataType
    {
        #region Properties

        public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

        public uint Size => _dataType.Size;

        #endregion
    }
}

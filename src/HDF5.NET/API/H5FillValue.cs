namespace HDF5.NET
{
    public partial class H5FillValue
    {
        #region Properties

        public byte[]? Value => _fillValue.Value?.ToArray();

        #endregion
    }
}

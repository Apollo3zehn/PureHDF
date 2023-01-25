namespace HDF5.NET
{
    partial class H5FillValue
    {
        #region Fields

        private readonly FillValueMessage _fillValue;

        #endregion

        #region Constructors

        internal H5FillValue(FillValueMessage fillValue)
        {
            _fillValue = fillValue;
        }

        #endregion
    }
}

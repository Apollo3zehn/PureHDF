namespace HDF5.NET
{
    partial class H5DataLayout
    {
        #region Fields

        private readonly DataLayoutMessage _dataLayout;

        #endregion

        #region Constructors

        internal H5DataLayout(DataLayoutMessage dataLayout)
        {
            _dataLayout = dataLayout;
        }

        #endregion
    }
}

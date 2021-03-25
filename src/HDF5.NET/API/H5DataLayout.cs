namespace HDF5.NET
{
    public class H5DataLayout
    {
        #region Fields

        private DataLayoutMessage _dataLayout;

        #endregion

        #region Properties

        public H5DataLayoutClass Class => (H5DataLayoutClass)_dataLayout.LayoutClass;

        #endregion

        #region Constructors

        internal H5DataLayout(DataLayoutMessage dataLayout)
        {
            _dataLayout = dataLayout;
        }

        #endregion
    }
}

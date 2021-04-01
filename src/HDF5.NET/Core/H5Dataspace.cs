namespace HDF5.NET
{
    partial class H5Dataspace
    {
        #region Fields

        private DataspaceMessage _dataspace;

        #endregion

        #region Constructors

        internal H5Dataspace(DataspaceMessage dataspace)
        {
            _dataspace = dataspace;
        }

        #endregion
    }
}

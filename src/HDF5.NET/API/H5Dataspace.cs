namespace HDF5.NET
{
    public class H5Dataspace
    {
        #region Fields

        private DataspaceMessage _dataspace;

        #endregion

        #region Properties

        public byte Rank => _dataspace.Rank;

        public H5DataspaceType Type => (H5DataspaceType)_dataspace.Type;

        public ulong[] Dimensions => _dataspace.DimensionSizes;

        public ulong[] MaxDimensions => _dataspace.DimensionMaxSizes;

        #endregion

        #region Constructors

        internal H5Dataspace(DataspaceMessage dataspace)
        {
            _dataspace = dataspace;
        }

        #endregion
    }
}

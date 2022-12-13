namespace HDF5.NET
{
    public partial class H5Dataspace
    {
        #region Properties

        public byte Rank => _dataspace.Rank;

        public H5DataspaceType Type => (H5DataspaceType)_dataspace.Type;

        public ulong[] Dimensions => _dataspace.DimensionSizes.ToArray();

        public ulong[] MaxDimensions => _dataspace.DimensionMaxSizes.ToArray();

        #endregion
    }
}

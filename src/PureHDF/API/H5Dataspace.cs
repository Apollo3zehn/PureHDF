namespace PureHDF
{
    /// <summary>
    /// An HDF5 dataspace.
    /// </summary>
    public partial class H5Dataspace
    {
        #region Properties

        /// <summary>
        /// Gets the dataspace rank.
        /// </summary>
        public byte Rank => _dataspace.Rank;

        /// <summary>
        /// Gets the dataspace type.
        /// </summary>
        public H5DataspaceType Type => (H5DataspaceType)_dataspace.Type;

        /// <summary>
        /// Gets the dataspace dimensions.
        /// </summary>
        public ulong[] Dimensions => _dataspace.DimensionSizes.ToArray();

        /// <summary>
        /// Gets the maximum dataspace dimensions.
        /// </summary>
        public ulong[] MaxDimensions => _dataspace.DimensionMaxSizes.ToArray();

        #endregion
    }
}

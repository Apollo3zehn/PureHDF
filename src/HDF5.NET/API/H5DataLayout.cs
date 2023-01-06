namespace HDF5.NET
{
    /// <summary>
    /// An HDF5 data layout.
    /// </summary>
    public partial class H5DataLayout
    {
        #region Properties

        /// <summary>
        /// Gets the data layout class.
        /// </summary>
        public H5DataLayoutClass Class => (H5DataLayoutClass)_dataLayout.LayoutClass;

        /// <summary>
        /// Gets the chunk dimensions.
        /// </summary>
        public ulong[] ChunkDimensions
        {
            get
            {
// TODO: Logic for non-chunked datsets is missing. Check for code duplication here: 
// TODO: https://github.com/Apollo3zehn/HDF5.NET/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/HDF5.NET/Core/H5D/H5D_Chunk.cs#L167
// TODO: https://github.com/Apollo3zehn/HDF5.NET/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/HDF5.NET/Core/H5D/H5D_Chunk4.cs#L28

                var rawChunkDims = _dataLayout switch
                {
                    DataLayoutMessage4 layout4 => ((ChunkedStoragePropertyDescription4)layout4.Properties).DimensionSizes.ToArray(),
                    DataLayoutMessage3 layout3 => ((ChunkedStoragePropertyDescription3)layout3.Properties).DimensionSizes.Select(value => (ulong)value).ToArray(),
                    DataLayoutMessage12 layout12 => layout12.DimensionSizes.Select(value => (ulong)value).ToArray(),
                    _ => throw new Exception("Unsupported data layout message.")
                };

                return rawChunkDims[..^1].ToArray();
            }
        }

        #endregion
    }
}

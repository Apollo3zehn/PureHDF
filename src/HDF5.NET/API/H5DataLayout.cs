namespace HDF5.NET
{
    public partial class H5DataLayout
    {
        #region Properties

        public H5DataLayoutClass Class => (H5DataLayoutClass)_dataLayout.LayoutClass;

        public ulong[] ChunkDimensions
        {
            get
            {
#warning Logic for non-chunked datsets is missing. Check for code duplication here: 
#warning https://github.com/Apollo3zehn/HDF5.NET/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/HDF5.NET/Core/H5D/H5D_Chunk.cs#L167
#warning https://github.com/Apollo3zehn/HDF5.NET/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/HDF5.NET/Core/H5D/H5D_Chunk4.cs#L28

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

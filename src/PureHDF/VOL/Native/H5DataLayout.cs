namespace PureHDF;

internal class H5DataLayout : IH5DataLayout
{
    #region Fields

    private readonly DataLayoutMessage _dataLayout;

    #endregion

    #region Properties

    public H5DataLayoutClass Class => (H5DataLayoutClass)_dataLayout.LayoutClass;

    public ulong[] ChunkDimensions
    {
        get
        {
            // TODO: Check for code duplication here: 
            // https://github.com/Apollo3zehn/PureHDF/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/PureHDF/Core/H5D/H5D_Chunk.cs#L167
            // https://github.com/Apollo3zehn/PureHDF/blob/b54eab46d3c063f8e3eb2abe5862f093b7a91c0d/src/PureHDF/Core/H5D/H5D_Chunk4.cs#L28

            var rawChunkDims = _dataLayout switch
            {
                DataLayoutMessage4 layout4 when layout4.LayoutClass == LayoutClass.Chunked
                    => ((ChunkedStoragePropertyDescription4)layout4.Properties).DimensionSizes.ToArray(),

                DataLayoutMessage3 layout3
                    => ((ChunkedStoragePropertyDescription3)layout3.Properties).DimensionSizes.Select(value => (ulong)value).ToArray(),

                DataLayoutMessage12 layout12
                    => layout12.DimensionSizes.Select(value => (ulong)value).ToArray(),

                _ => throw new Exception("Unsupported data layout.")
            };

            return rawChunkDims[..^1].ToArray();
        }
    }

    #endregion

    #region Constructors

    internal H5DataLayout(DataLayoutMessage dataLayout)
    {
        _dataLayout = dataLayout;
    }

    #endregion
}
namespace PureHDF;

/// <summary>
/// A dataset.
/// </summary>
public class H5Dataset : H5AttributableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class.
    /// </summary>
    /// <param name="data">The dataset data.</param>
    /// <param name="dimensions">The dataset dimensions.</param>
    /// <param name="chunkDimensions">The dataset's chunk dimenions.</param>
    /// <param name="datasetAccess">The dataset access properties.</param>
    public H5Dataset(
        object data,
        ulong[]? dimensions = default,
        uint[]? chunkDimensions = default,
        H5DatasetAccess datasetAccess = default)
    {
        Data = data;
        Dimensions = dimensions;
        ChunkDimensions = chunkDimensions;
        DatasetAccess = datasetAccess;
    }

    internal object Data { get; init; }
    
    internal ulong[]? Dimensions { get; init; }

    internal uint[]? ChunkDimensions { get; init; }

    internal H5DatasetAccess DatasetAccess { get; init; }
}
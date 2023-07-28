namespace PureHDF;

/// <summary>
/// A dataset.
/// </summary>
public class H5Dataset : H5AttributableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Dataset"/> class.
    /// </summary>
    /// <param name="data">The attribute data.</param>
    /// <param name="dimensions">The attribute dimensions.</param>
    public H5Dataset(
        object data,
        ulong[]? dimensions = default)
    {
        Data = data;
        Dimensions = dimensions;
    }

    internal object Data { get; init; }
    
    internal ulong[]? Dimensions { get; init; }
}
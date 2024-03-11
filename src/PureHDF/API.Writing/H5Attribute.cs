namespace PureHDF;

/// <summary>
/// An attribute.
/// </summary>
public class H5Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Attribute"/> class.
    /// </summary>
    /// <param name="data">The attribute data.</param>
    /// <param name="dimensions">The attribute dimensions.</param>
    public H5Attribute(
        object data,
        ulong[]? dimensions = default)
    {
        Data = data;
        Dimensions = dimensions;
    }

    internal object Data { get; init; }

    internal ulong[]? Dimensions { get; init; }
}
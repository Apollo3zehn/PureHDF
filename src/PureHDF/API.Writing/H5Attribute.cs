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
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Attribute(
        object data,
        ulong[]? dimensions = default,
        H5OpaqueInfo? opaqueInfo = default)
    {
        Data = data;
        Dimensions = dimensions;
        OpaqueInfo = opaqueInfo;
    }

    internal object Data { get; init; }

    internal ulong[]? Dimensions { get; init; }

    internal H5OpaqueInfo? OpaqueInfo { get; init; }
}
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
        Type = data.GetType();
        Data = data;
        Dimensions = dimensions;
        OpaqueInfo = opaqueInfo;
    }

    internal H5Attribute(
        Type type,
        object? data,
        ulong[]? dimensions,
        H5OpaqueInfo? opaqueInfo,
        bool isNullDataspace)
    {
        Type = type;
        Data = data;
        Dimensions = dimensions;
        OpaqueInfo = opaqueInfo;
        IsNullDataspace = isNullDataspace;
    }

    internal Type Type { get; }

    internal object? Data { get; }

    internal ulong[]? Dimensions { get; }

    internal H5OpaqueInfo? OpaqueInfo { get; }

    internal bool IsNullDataspace { get; }
}

/// <summary>
/// An attribute.
/// </summary>
public class H5Attribute<T> : H5Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Attribute"/> class. Use this constructor to preserve specific type information, e.g. for nullable value types like `int?`.
    /// </summary>
    /// <param name="data">The attribute data.</param>
    /// <param name="dimensions">The attribute dimensions.</param>
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Attribute(
        T data,
        ulong[]? dimensions = default,
        H5OpaqueInfo? opaqueInfo = default)
        : base(
            typeof(T),
            data,
            dimensions,
            opaqueInfo,
            isNullDataspace: false)
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="H5Attribute"/> class with a null dataspace.
    /// </summary>
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    public H5Attribute(
        H5OpaqueInfo? opaqueInfo = default)
        : base(
            typeof(T),
            data: default,
            dimensions: default,
            opaqueInfo,
            isNullDataspace: true)
    {
        //
    }
}
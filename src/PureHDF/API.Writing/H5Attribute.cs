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
    /// <param name="stringInfo">How the strings of this attribute are stored, in place of the file-wide string options.</param>
    public H5Attribute(
        object data,
        ulong[]? dimensions = default,
        H5OpaqueInfo? opaqueInfo = default,
        H5StringInfo? stringInfo = default)
    {
        Type = data.GetType();
        Data = data;
        Dimensions = dimensions;
        OpaqueInfo = opaqueInfo;
        StringInfo = stringInfo;
    }

    internal H5Attribute(
        Type type,
        object? data,
        ulong[]? dimensions,
        H5OpaqueInfo? opaqueInfo,
        H5StringInfo? stringInfo,
        bool isNullDataspace)
    {
        Type = type;
        Data = data;
        Dimensions = dimensions;
        OpaqueInfo = opaqueInfo;
        StringInfo = stringInfo;
        IsNullDataspace = isNullDataspace;
    }

    internal Type Type { get; }

    internal object? Data { get; }

    internal ulong[]? Dimensions { get; }

    internal H5OpaqueInfo? OpaqueInfo { get; }

    internal H5StringInfo? StringInfo { get; }

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
    /// <param name="stringInfo">How the strings of this attribute are stored, in place of the file-wide string options.</param>
    public H5Attribute(
        T data,
        ulong[]? dimensions = default,
        H5OpaqueInfo? opaqueInfo = default,
        H5StringInfo? stringInfo = default)
        : base(
            typeof(T),
            data,
            dimensions,
            opaqueInfo,
            stringInfo,
            isNullDataspace: false)
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="H5Attribute"/> class with a null dataspace.
    /// </summary>
    /// <param name="opaqueInfo">Set this paramter to a non-null value to treat data of type byte[] as opaque.</param>
    /// <param name="stringInfo">How the strings of this attribute are stored, in place of the file-wide string options.</param>
    public H5Attribute(
        H5OpaqueInfo? opaqueInfo = default,
        H5StringInfo? stringInfo = default)
        : base(
            typeof(T),
            data: default,
            dimensions: default,
            opaqueInfo,
            stringInfo,
            isNullDataspace: true)
    {
        //
    }
}
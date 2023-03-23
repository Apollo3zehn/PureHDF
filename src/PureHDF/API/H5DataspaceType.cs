namespace PureHDF;

/// <summary>
/// The dataspace type.
/// </summary>
public enum H5DataspaceType : byte
{
    /// <summary>
    /// A scalar dataspace.
    /// </summary>
    Scalar = 0,

    /// <summary>
    /// A simple dataspace.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// A null-dataspace.
    /// </summary>
    Null = 2
}
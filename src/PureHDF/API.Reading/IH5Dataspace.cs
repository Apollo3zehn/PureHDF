namespace PureHDF;

/// <summary>
/// An HDF5 dataspace.
/// </summary>
public interface IH5Dataspace
{
    /// <summary>
    /// Gets the dataspace rank.
    /// </summary>
    byte Rank { get; }

    /// <summary>
    /// Gets the dataspace type.
    /// </summary>
    H5DataspaceType Type { get; }

    /// <summary>
    /// Gets the dataspace dimensions.
    /// </summary>
    ulong[] Dimensions { get; }

    /// <summary>
    /// Gets the maximum dataspace dimensions.
    /// </summary>
    ulong[] MaxDimensions { get; }
}
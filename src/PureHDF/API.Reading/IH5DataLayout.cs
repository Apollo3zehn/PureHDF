namespace PureHDF;

/// <summary>
/// An HDF5 data layout.
/// </summary>
public interface IH5DataLayout
{
    /// <summary>
    /// Gets the data layout class.
    /// </summary>
    H5DataLayoutClass Class { get; }

    /// <summary>
    /// Gets the chunk dimensions.
    /// </summary>
    ulong[] ChunkDimensions { get; }
}
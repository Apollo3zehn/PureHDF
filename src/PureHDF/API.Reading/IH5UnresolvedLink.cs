namespace PureHDF;

/// <summary>
/// An HDF5 link that could not be resolved.
/// </summary>
public interface IH5UnresolvedLink
{
    /// <summary>
    /// Gets the link name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an exception that indicates the reason why the link could not be resolved.
    /// </summary>
    Exception? Reason { get; }
}

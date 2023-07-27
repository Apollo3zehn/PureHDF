namespace PureHDF;

/// <summary>
/// A base class HDF5 objects.
/// </summary>
public interface IH5Object
{
    /// <summary>
    /// Gets the name.
    /// </summary>
    string Name { get; }
}
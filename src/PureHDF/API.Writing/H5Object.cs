namespace PureHDF;

/// <summary>
/// A base class for HDF5 objects.
/// </summary>
public class H5Object
{
    /// <summary>
    /// A map of attributes that belong to this object.
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();
}
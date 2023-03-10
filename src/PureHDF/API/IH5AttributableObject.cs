namespace PureHDF;

/// <summary>
/// A base class for types that can hold HDF5 attributes.
/// </summary>
public interface IH5AttributableObject : IH5Object
{
    /// <summary>
    /// Gets an enumerable of the available attributes.
    /// </summary>
    IEnumerable<IH5Attribute> Attributes { get; }

    /// <summary>
    /// Checks if the attribute with the specified <paramref name="name"/> exist.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>A boolean which indicates if the attribute exists.</returns>
    IH5Attribute Attribute(string name);

    /// <summary>
    /// Gets the attribute named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>The requested attribute.</returns>
    bool AttributeExists(string name);
}
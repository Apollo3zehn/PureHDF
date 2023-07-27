namespace PureHDF;

/// <summary>
/// A base class for types that can hold HDF5 attributes.
/// </summary>
public interface IH5AttributableObject : IH5Object
{
    /// <summary>
    /// Gets an enumerable of the available attributes.
    /// </summary>
    IEnumerable<IH5Attribute> Attributes();

    /// <summary>
    /// Gets an enumerable of the available attributes.
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// </summary>
    Task<IEnumerable<IH5Attribute>> AttributesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the attribute with the specified <paramref name="name"/> exist.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>A boolean which indicates if the attribute exists.</returns>
    IH5Attribute Attribute(string name);

    /// <summary>
    /// Checks if the attribute with the specified <paramref name="name"/> exist.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A boolean which indicates if the attribute exists.</returns>
    Task<IH5Attribute> AttributeAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the attribute named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <returns>The requested attribute.</returns>
    bool AttributeExists(string name);

    /// <summary>
    /// Gets the attribute named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the attribute.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested attribute.</returns>
    Task<bool> AttributeExistsAsync(string name, CancellationToken cancellationToken = default);
}
namespace PureHDF;

/// <summary>
/// An HDF5 group.
/// </summary>
public interface IH5Group : IH5AttributableObject
{
    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    bool LinkExists(string path);

    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    Task<bool> LinkExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(string path);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(NativeObjectReference reference);

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    Task<IH5Object> GetAsync(NativeObjectReference reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an enumerable of the available children.
    /// </summary>
    /// <returns>An enumerable of the available children.</returns>
    IEnumerable<IH5Object> Children();

    /// <summary>
    /// Gets an enumerable of the available children.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>An enumerable of the available children.</returns>
    Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default);
}
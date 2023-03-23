namespace PureHDF.VOL.Native;

/// <summary>
/// An HDF5 group.
/// </summary>
public interface INativeGroup : IH5Group
{
    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    bool LinkExists(string path, H5LinkAccess linkAccess);

    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    Task<bool> LinkExistsAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(string path, H5LinkAccess linkAccess);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    Task<IH5Object> GetAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess);

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    Task<IH5Object> GetAsync(H5ObjectReference reference, H5LinkAccess linkAccess, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>An enumerable of the available children.</returns>
    IEnumerable<IH5Object> Children(H5LinkAccess linkAccess);

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>An enumerable of the available children.</returns>
    Task<IEnumerable<IH5Object>> ChildrenAsync(H5LinkAccess linkAccess, CancellationToken cancellationToken = default);
}
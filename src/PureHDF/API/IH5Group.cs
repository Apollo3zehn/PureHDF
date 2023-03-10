namespace PureHDF;

/// <summary>
/// An HDF5 group.
/// </summary>
public interface IH5Group : IH5AttributableObject
{
    /// <summary>
    /// Gets an enumerable of the available children.
    /// </summary>
    IEnumerable<IH5Object> Children { get; }

    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    bool LinkExists(string path, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(string path, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    T Get<T>(string path, H5LinkAccess linkAccess = default) where T : IH5Object;

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    IH5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    T Get<T>(H5ObjectReference reference, H5LinkAccess linkAccess = default) where T : IH5Object;

    /// <summary>
    /// Gets the group that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested group.</returns>
    IH5Group Group(string path, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets the dataset that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested dataset.</returns>
    IH5Dataset Dataset(string path, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets the commited data type that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested commited data type.</returns>
    IH5CommitedDatatype CommitedDatatype(string path, H5LinkAccess linkAccess = default);

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>An enumerable of the available children.</returns>
    IEnumerable<IH5Object> GetChildren(H5LinkAccess linkAccess = default);
}
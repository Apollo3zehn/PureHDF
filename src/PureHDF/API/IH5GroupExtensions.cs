namespace PureHDF;

/// <summary>
/// Defines extensions methods for the <see cref="IH5Group" /> type.
/// </summary>
public static class IH5GroupExtensions
{
    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to get the object from.</param>
    /// <param name="path">The path of the object.</param>
    /// <returns>The requested object.</returns>
    public static T Get<T>(this IH5Group group, string path) where T : IH5Object
    {
        return (T)group.Get(path);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to get the object from.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this IH5Group group, 
        string path, 
        CancellationToken cancellationToken = default) where T : IH5Object
    {
        return (T)await group.GetAsync(path, cancellationToken);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to get the object from.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <returns>The requested object.</returns>
    public static T Get<T>(
        this IH5Group group, 
        H5ObjectReference reference)
        where T : IH5Object
    {
        return (T)group.Get(reference);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to get the object from.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this IH5Group group, 
        H5ObjectReference reference, 
        CancellationToken cancellationToken = default)
        where T : IH5Object
    {
        return (T)await group.GetAsync(reference, cancellationToken);
    }
}
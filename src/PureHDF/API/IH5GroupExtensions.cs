namespace PureHDF;

// TODO: properly implement async

/// <summary>
/// Defines extensions methods for the <see cref="IH5Group" /> type.
/// </summary>
public static class IH5GroupExtensions
{
    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
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
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this IH5Group group, 
        string path, 
        CancellationToken cancellationToken = default) where T : IH5Object
    {
        return (T)await group
            .GetAsync(path, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <returns>The requested object.</returns>
    public static T Get<T>(
        this IH5Group group, 
        NativeObjectReference reference)
        where T : IH5Object
    {
        return (T)group.Get(reference);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this IH5Group group,
        NativeObjectReference reference, 
        CancellationToken cancellationToken = default)
        where T : IH5Object
    {
        return (T)await group
            .GetAsync(reference, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the group that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <returns>The requested group.</returns>
    public static IH5Group Group(this IH5Group group, string path)
    {
        var link = group.Get(path);

        if (link is not IH5Group linkedGroup)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(IH5Group)} because it is of type {link.GetType().Name}.");

        return linkedGroup;
    }

    /// <summary>
    /// Gets the group that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested group.</returns>
    public static Task<IH5Group> GroupAsync(this IH5Group group, string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.Group(path));
    }

    /// <summary>
    /// Gets the dataset that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <returns>The requested dataset.</returns>
    public static IH5Dataset Dataset(this IH5Group group, string path)
    {
        var link = group.Get(path);

        if (link is not IH5Dataset castedLink)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(IH5Dataset)} because it is of type {link.GetType().Name}.");

        return castedLink;
    }

    /// <summary>
    /// Gets the dataset that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested dataset.</returns>
    public static Task<IH5Dataset> DatasetAsync(this IH5Group group, string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.Dataset(path));
    }

    /// <summary>
    /// Gets the commited data type that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <returns>The requested commited data type.</returns>
    public static IH5CommitedDatatype CommitedDatatype(this IH5Group group, string path)
    {
        var link = group.Get(path);

        if (link is not IH5CommitedDatatype castedLink)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(IH5CommitedDatatype)} because it is of type {link.GetType().Name}.");

        return castedLink;
    }

    /// <summary>
    /// Gets the commited data type that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested commited data type.</returns>
    public static Task<IH5CommitedDatatype> CommitedDatatypeAsync(this IH5Group group, string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.CommitedDatatype(path));
    }
}
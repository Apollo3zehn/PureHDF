namespace PureHDF.VOL.Native;

/// <summary>
/// Defines extensions methods for the <see cref="INativeGroup" /> type.
/// </summary>
public static class INativeGroupExtensions
{
    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    public static T Get<T>(
        this INativeGroup group, 
        string path, 
        H5LinkAccess linkAccess) where T : IH5Object
    {
        return (T)group.Get(path, linkAccess);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this INativeGroup group,
        string path, 
        H5LinkAccess linkAccess, 
        CancellationToken cancellationToken = default) where T : IH5Object
    {
        return (T)await group
            .GetAsync(path, linkAccess, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    public static T Get<T>(
        this INativeGroup group, 
        NativeObjectReference reference, 
        H5LinkAccess linkAccess)
        where T : IH5Object
    {
        return (T)group.Get(reference, linkAccess);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The return type of the object.</typeparam>
    /// <param name="group">The group to operate on.</param>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public static async Task<T> GetAsync<T>(
        this INativeGroup group, 
        NativeObjectReference reference, 
        H5LinkAccess linkAccess, 
        CancellationToken cancellationToken = default)
        where T : IH5Object
    {
        return (T)await group
            .GetAsync(reference, linkAccess, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the group that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested group.</returns>
    public static INativeGroup Group(this INativeGroup group, string path, H5LinkAccess linkAccess)
    {
        var link = group.Get(path, linkAccess);

        if (link is not INativeGroup linkedGroup)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(INativeGroup)} because it is of type {link.GetType().Name}.");

        return linkedGroup;
    }

    /// <summary>
    /// Gets the group that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested group.</returns>
    public static Task<INativeGroup> GroupAsync(this INativeGroup group, string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.Group(path, linkAccess));
    }

    /// <summary>
    /// Gets the dataset that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested dataset.</returns>
    public static IH5Dataset Dataset(this INativeGroup group, string path, H5LinkAccess linkAccess)
    {
        var link = group.Get(path, linkAccess);

        if (link is not IH5Dataset castedLink)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(IH5Dataset)} because it is of type {link.GetType().Name}.");

        return castedLink;
    }

    /// <summary>
    /// Gets the dataset that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested dataset.</returns>
    public static Task<IH5Dataset> DatasetAsync(this INativeGroup group, string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.Dataset(path, linkAccess));
    }

    /// <summary>
    /// Gets the commited data type that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested commited data type.</returns>
    public static IH5CommitedDatatype CommitedDatatype(this INativeGroup group, string path, H5LinkAccess linkAccess)
    {
        var link = group.Get(path, linkAccess);

        if (link is not IH5CommitedDatatype castedLink)
            throw new Exception($"The requested link exists but cannot be casted to {nameof(IH5CommitedDatatype)} because it is of type {link.GetType().Name}.");

        return castedLink;
    }

    /// <summary>
    /// Gets the commited data type that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="group">The group to operate on.</param>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested commited data type.</returns>
    public static Task<IH5CommitedDatatype> CommitedDatatypeAsync(this INativeGroup group, string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(group.CommitedDatatype(path, linkAccess));
    }
}
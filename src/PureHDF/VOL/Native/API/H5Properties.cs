namespace PureHDF.VOL.Native;

/// <summary>
/// A structure which controls how the dataset is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html">hdfgroup.org</seealso>
/// </summary>
/// <param name="ExternalFilePrefix">The external dataset storage file prefix. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11">hdfgroup.org</seealso>.</param>
/// <param name="VirtualPrefix">The prefix to be applied to VDS source file paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12">hdfgroup.org</seealso>.</param>
/// <param name="ChunkCacheFactory">The factory to create the chunk cache. If <see langword="null"/>, the factory of the <see cref="INativeFile.ChunkCacheFactory"/> property is used.</param>
public readonly record struct H5DatasetAccess(
    string? ExternalFilePrefix = default,
    string? VirtualPrefix = default,
    Func<IChunkCache>? ChunkCacheFactory = default);

/// <summary>
/// A structure which controls how the link is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html">hdfgroup.org</seealso>.
/// <param name="ExternalLinkPrefix">The prefix to be applied to external link paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html#gafa5eced13ba3a00cdd65669626dc7294">hdfgroup.org</seealso>.</param>
/// </summary>
public readonly record struct H5LinkAccess(string? ExternalLinkPrefix = default);
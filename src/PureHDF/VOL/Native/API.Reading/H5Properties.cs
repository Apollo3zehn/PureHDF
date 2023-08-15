namespace PureHDF.VOL.Native;

/// <summary>
/// A structure which controls how the dataset is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html">hdfgroup.org</seealso>
/// </summary>
/// <param name="ExternalFilePrefix">The external dataset storage file prefix. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11">hdfgroup.org</seealso>.</param>
/// <param name="VirtualPrefix">The prefix to be applied to VDS source file paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12">hdfgroup.org</seealso>.</param>
/// <param name="ChunkCache">The chunk cache used for reading. If <see langword="null"/>, the value of the <see cref="NativeFile.ChunkCacheFactory"/> property is used instead.</param>
public readonly partial record struct H5DatasetAccess(
    string? ExternalFilePrefix = default,
    string? VirtualPrefix = default,
    IReadingChunkCache? ChunkCache = default);

/// <summary>
/// A structure which controls how the link is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html">hdfgroup.org</seealso>.
/// <param name="ExternalLinkPrefix">The prefix to be applied to external link paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___l_a_p_l.html#gafa5eced13ba3a00cdd65669626dc7294">hdfgroup.org</seealso>.</param>
/// </summary>
public readonly record struct H5LinkAccess(string? ExternalLinkPrefix = default);
namespace PureHDF.VOL.Native;

/// <summary>
/// A structure which controls how the dataset is created. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_c_p_l.html">hdfgroup.org</seealso>
/// </summary>
/// <param name="ChunkCache">The chunk cache used for writing. If <see langword="null"/>, the value of the <see cref="ChunkCache.DefaultWritingChunkCacheFactory"/> property is used instead.</param>
/// <param name="Filters">A list of filters and their options to be applied to a chunk being written to the stream. If <see langword="null"/>, the value of the <see cref="H5WriteOptions.Filters"/> property is used instead.</param>
public readonly partial record struct H5DatasetCreation(
    IWritingChunkCache? ChunkCache = default,
    List<H5Filter>? Filters = default);
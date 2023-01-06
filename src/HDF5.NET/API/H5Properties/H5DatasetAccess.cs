namespace HDF5.NET
{
    /// <summary>
    /// A structure which controls how the dataset is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html">hdfgroup.org</seealso>
    /// </summary>
    public struct H5DatasetAccess
    {
        /// <summary>
        /// Gets the external dataset storage file prefix. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#gad487f84157fd0944cbe1cbd4dea4e1b8">hdfgroup.org</seealso>.
        /// </summary>
        public string? ExternalFilePrefix { get; init; }

        /// <summary>
        /// Gets the factory to create the chunk cache. If <see langword="null"/>, the factory of the <see cref="H5File.ChunkCacheFactory"/> property is used.
        /// </summary>
        public Func<IChunkCache>? ChunkCacheFactory { get; init; }
    }
}

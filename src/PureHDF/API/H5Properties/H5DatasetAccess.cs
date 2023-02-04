namespace PureHDF
{
    /// <summary>
    /// A structure which controls how the dataset is accessed. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html">hdfgroup.org</seealso>
    /// </summary>
    public readonly record struct H5DatasetAccess
    {
        /// <summary>
        /// Gets or initializes the external dataset storage file prefix. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title11">hdfgroup.org</seealso>.
        /// </summary>
        public string? ExternalFilePrefix { get; init; }

        /// <summary>
        /// Gets or initializes prefix to be applied to VDS source file paths. Reference: <seealso href="https://docs.hdfgroup.org/hdf5/v1_10/group___d_a_p_l.html#title12">hdfgroup.org</seealso>.
        /// </summary>
        public string? VirtualFilePrefix { get; init; }

        /// <summary>
        /// Gets or initializes the factory to create the chunk cache. If <see langword="null"/>, the factory of the <see cref="H5File.ChunkCacheFactory"/> property is used.
        /// </summary>
        public Func<IChunkCache>? ChunkCacheFactory { get; init; }
    }
}

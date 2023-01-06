namespace HDF5.NET
{
    /// <summary>
    /// Specifies the filter identifier.
    /// </summary>
    public enum H5FilterID : ushort
    {
        /// <summary>
        /// No filter.
        /// </summary>
        NA = 0,

        /// <summary>
        /// Deflation like gzip.
        /// </summary>
        Deflate = 1,

        /// <summary>
        /// Shuffle the data.
        /// </summary>
        Shuffle = 2,

        /// <summary>
        /// Fletcher32 checksum of EDC.
        /// </summary>
        Fletcher32 = 3,

        /// <summary>
        /// Szip compression
        /// </summary>
        Szip = 4,

        /// <summary>
        /// NBit compression.
        /// </summary>
        Nbit = 5,

        /// <summary>
        /// Scale+offset compression.
        /// </summary>
        ScaleOffset = 6
    }
}

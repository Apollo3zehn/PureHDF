namespace PureHDF
{
    /// <summary>
    /// Specifies the data layout class.
    /// </summary>
    public enum H5DataLayoutClass : byte
    {
        /// <summary>
        /// The data is stored within the object's metadata.
        /// </summary>
        Compact = 0,

        /// <summary>
        /// THe data is stored as one contiguous block of data.
        /// </summary>
        Contiguous = 1,

        /// <summary>
        /// The data is stored in chunks.
        /// </summary>
        Chunked = 2,

        /// <summary>
        /// The data is a virtual view composed of other datasets.
        /// </summary>
        VirtualStorage = 3
    }
}

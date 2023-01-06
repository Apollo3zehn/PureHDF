namespace HDF5.NET
{
    /// <summary>
    /// Represents a single unit of data to be selected.
    /// </summary>
    public struct Step
    {
        /// <summary>
        /// Gets the data coordinates.
        /// </summary>
        public ulong[] Coordinates { get; set; }

        /// <summary>
        /// Gets the number of elements to select along the fastest changing dimension.
        /// </summary>
        public ulong ElementCount { get; set; }
    }
}
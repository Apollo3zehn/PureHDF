namespace PureHDF
{
    /// <summary>
    /// Represents a single unit of data to be selected.
    /// </summary>
    public struct Step
    {
        /// <summary>
        /// Gets or sets the data coordinates.
        /// </summary>
        public ulong[] Coordinates { get; set; }

        /// <summary>
        /// Gets or sets the number of elements to select along the fastest changing dimension.
        /// </summary>
        public ulong ElementCount { get; set; }
    }
}
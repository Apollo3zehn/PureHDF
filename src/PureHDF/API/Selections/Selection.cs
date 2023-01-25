namespace PureHDF
{
    /// <summary>
    /// A base class which represents a selection.
    /// </summary>
    public abstract class Selection
    {
        /// <summary>
        /// Gets the total number of elements which is used to preallocate the returned buffer.
        /// </summary>
        public abstract ulong TotalElementCount { get; }

        /// <summary>
        /// The walk function is used to walk through the dataset and select the requested data.
        /// </summary>
        /// <param name="limits">The dataset dimensions.</param>
        /// <returns>An enumerable which provides a sequence of steps to select the requested data.</returns>
        public abstract IEnumerable<Step> Walk(ulong[] limits);
    }
}
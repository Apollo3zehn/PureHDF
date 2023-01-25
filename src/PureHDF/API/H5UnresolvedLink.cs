namespace PureHDF
{
    /// <summary>
    /// An HDF5 link that could not be resolved.
    /// </summary>
    public partial class H5UnresolvedLink : H5Object
    {
        #region Properties

        /// <summary>
        /// Gets an exception that indicates the reason why the link could not be resolved.
        /// </summary>
        public Exception? Reason { get; }

        #endregion
    }
}

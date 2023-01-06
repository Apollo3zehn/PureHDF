namespace HDF5.NET
{
    /// <summary>
    /// A base class HDF5 objects.
    /// </summary>
    public abstract partial class H5Object
    {
        #region Properties

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name => Reference.Name;

        #endregion
    }
}

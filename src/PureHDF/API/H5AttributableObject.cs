namespace PureHDF
{
    /// <summary>
    /// A base class for types that can hold HDF5 attributes.
    /// </summary>
    public abstract partial class H5AttributableObject : H5Object
    {
        #region Properties

        /// <summary>
        /// Gets an enumerable of the available attributes.
        /// </summary>
        public IEnumerable<H5Attribute> Attributes => EnumerateAttributes();

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the attribute with the specified <paramref name="name"/> exist.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        /// <returns>A boolean which indicates if the attribute exists.</returns>
        public bool AttributeExists(string name)
        {
            return TryGetAttributeMessage(name, out var _);
        }

        /// <summary>
        /// Gets the attribute named <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        /// <returns>The requested attribute.</returns>
        public H5Attribute Attribute(string name)
        {
            if (!TryGetAttributeMessage(name, out var attributeMessage))
                throw new Exception($"Could not find attribute '{name}'.");

            return new H5Attribute(Context, attributeMessage);
        }

        #endregion
    }
}

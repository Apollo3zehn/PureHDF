namespace HDF5.NET
{
    public abstract partial class H5AttributableObject : H5Object
    {
        #region Properties

        public IEnumerable<H5Attribute> Attributes => EnumerateAttributes();

        #endregion

#warning Add File Property?

        #region Methods

        public bool AttributeExists(string name)
        {
            return TryGetAttributeMessage(name, out var _);
        }

        public H5Attribute Attribute(string name)
        {
            if (!TryGetAttributeMessage(name, out var attributeMessage))
                throw new Exception($"Could not find attribute '{name}'.");

            return new H5Attribute(attributeMessage, Context.Superblock);
        }

        #endregion
    }
}

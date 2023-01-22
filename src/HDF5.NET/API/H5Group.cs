namespace HDF5.NET
{
    /// <summary>
    /// An HDF5 group.
    /// </summary>
    public partial class H5Group : H5AttributableObject
    {
        #region Properties

        /// <summary>
        /// Gets an enumerable of the available children.
        /// </summary>
        public IEnumerable<H5Object> Children
            => GetChildren(new H5LinkAccess());

        #endregion

        #region Public

        /// <summary>
        /// Checks if the link with the specified <paramref name="path"/> exist.
        /// </summary>
        /// <param name="path">The path of the link.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>A boolean which indicates if the link exists.</returns>
        public bool LinkExists(string path, H5LinkAccess linkAccess = default)
        {
            return InternalLinkExists(path, linkAccess);
        }

        /// <summary>
        /// Gets the object that is at the given <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested object.</returns>
        public H5Object Get(string path, H5LinkAccess linkAccess = default)
        {
            return InternalGet(path, linkAccess)
                .Dereference();
        }

        /// <summary>
        /// Gets the object that is at the given <paramref name="path"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the object.</typeparam>
        /// <param name="path">The path of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested object.</returns>
        public T Get<T>(string path, H5LinkAccess linkAccess = default) where T : H5Object
        {
            return (T)Get(path, linkAccess);
        }

        /// <summary>
        /// Gets the object that is at the given <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested object.</returns>
        public H5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess = default)
        {
            if (Reference.Value == reference.Value)
                return this;

            return InternalGet(reference, linkAccess)
                .Dereference();
        }

        /// <summary>
        /// Gets the object that is at the given <paramref name="reference"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the object.</typeparam>
        /// <param name="reference">The reference of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested object.</returns>
        public T Get<T>(H5ObjectReference reference, H5LinkAccess linkAccess = default)
            where T : H5Object
        {
            return (T)Get(reference, linkAccess);
        }

        /// <summary>
        /// Gets the group that is at the given <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested group.</returns>
        public H5Group Group(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var group = link as H5Group;

            if (group is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Group)} because it is of type {link.GetType().Name}.");

            return group;
        }

        /// <summary>
        /// Gets the dataset that is at the given <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested dataset.</returns>
        public H5Dataset Dataset(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var castedLink = link as H5Dataset;

            if (castedLink is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Dataset)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        /// <summary>
        /// Gets the commited data type that is at the given <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path of the object.</param>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>The requested commited data type.</returns>
        public H5CommitedDatatype CommitedDatatype(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var castedLink = link as H5CommitedDatatype;

            if (castedLink is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5CommitedDatatype)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        /// <summary>
        /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
        /// </summary>
        /// <param name="linkAccess">The link access properties.</param>
        /// <returns>An enumerable of the available children.</returns>
        public IEnumerable<H5Object> GetChildren(H5LinkAccess linkAccess = default)
        {
            return this
                .EnumerateReferences(linkAccess)
                .Select(reference => reference.Dereference());
        }

        #endregion
    }
}

namespace HDF5.NET
{
    public partial class H5Group : H5AttributableObject
    {
        #region Properties

        public IEnumerable<H5Object> Children
            => GetChildren(new H5LinkAccess());

        #endregion

        #region Public

        public bool LinkExists(string path, H5LinkAccess linkAccess = default)
        {
            return InternalLinkExists(path, linkAccess);
        }

        public H5Object Get(string path, H5LinkAccess linkAccess = default)
        {
            return this
                .InternalGet(path, linkAccess)
                .Dereference();
        }

        public H5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess = default)
        {
            if (Reference.Value == reference.Value)
                return this;

            return this
                .InternalGet(reference, linkAccess)
                .Dereference();
        }

        public H5Group Group(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var group = link as H5Group;

            if (group is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Group)} because it is of type {link.GetType().Name}.");

            return group;
        }

        public H5Dataset Dataset(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var castedLink = link as H5Dataset;

            if (castedLink is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Dataset)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        public H5CommitedDatatype CommitedDatatype(string path, H5LinkAccess linkAccess = default)
        {
            var link = Get(path, linkAccess);
            var castedLink = link as H5CommitedDatatype;

            if (castedLink is null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5CommitedDatatype)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        public IEnumerable<H5Object> GetChildren(H5LinkAccess linkAccess = default)
        {
            return this
                .EnumerateReferences(linkAccess)
                .Select(reference => reference.Dereference());
        }

        #endregion
    }
}

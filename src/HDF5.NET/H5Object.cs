using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class H5Object : ILink
    {
        #region Fields

        private H5NamedReference _reference;
        private ObjectHeader? _header;
        private ObjectReferenceCountMessage? _objectReferenceCount;

        #endregion

        #region Constructors

        internal H5Object(H5Context context, H5NamedReference reference)
        {
            this.Context = context;
            _reference = reference;
        }

        internal H5Object(H5Context context, H5NamedReference reference, ObjectHeader header)
        {
            this.Context = context;
            _reference = reference;
            _header = header;
        }

        #endregion

        #region Properties

        public string Name => _reference.Name;

        public uint ReferenceCount => this.ObjectReferenceCount == null
            ? 1
            : this.ObjectReferenceCount.ReferenceCount;

        internal H5Context Context { get; }

        private ObjectReferenceCountMessage? ObjectReferenceCount
        {
            get
            {
                if (_objectReferenceCount == null)
                {
                    _objectReferenceCount = this.Header
                        .GetMessages<ObjectReferenceCountMessage>()
                        .FirstOrDefault();
                }

                return _objectReferenceCount;
            }
        }

        private protected ObjectHeader Header
        {
            get
            {
                if (_header == null && !this.Context.Superblock.IsUndefinedAddress(_reference.Value))
                {
                    this.Context.Reader.Seek((long)_reference.Value, SeekOrigin.Begin);
                    _header = ObjectHeader.Construct(this.Context);
                }

                return _header;
            }
        }

        #endregion
    }
}

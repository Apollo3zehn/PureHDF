using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class H5Object : ILink
    {
        #region Fields

        private ObjectHeader? _header;
        private ObjectReferenceCountMessage? _objectReferenceCount;

        #endregion

        #region Constructors

        internal H5Object(H5Context context, H5NamedReference reference)
        {
            this.Context = context;
            this.Reference = reference;
        }

        internal H5Object(H5Context context, H5NamedReference reference, ObjectHeader header)
        {
            this.Context = context;
            this.Reference = reference;
            _header = header;
        }

        #endregion

        #region Properties

        public string Name => this.Reference.Name;

        public uint ReferenceCount => this.ObjectReferenceCount == null
            ? 1
            : this.ObjectReferenceCount.ReferenceCount;

        public H5NamedReference Reference { get; }

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
                if (_header == null)
                {
                    this.Context.Reader.Seek((long)this.Reference.Value, SeekOrigin.Begin);
                    _header = ObjectHeader.Construct(this.Context);
                }

                return _header;
            }
        }

        #endregion
    }
}

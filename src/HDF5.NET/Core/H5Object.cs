namespace HDF5.NET
{
    abstract partial class H5Object
    {
        #region Fields

        private ObjectHeader? _header;
        private ObjectReferenceCountMessage? _objectReferenceCount;

        #endregion

        #region Constructors

        internal H5Object(H5Context context, NamedReference reference)
        {
            Context = context;
            Reference = reference;
        }

        internal H5Object(H5Context context, NamedReference reference, ObjectHeader header)
        {
            Context = context;
            Reference = reference;
            _header = header;
        }

        #endregion

        #region Properties

        internal H5Context Context { get; }

        internal uint ReferenceCount => GetReferenceCount();

        internal NamedReference Reference { get; set; }

        private ObjectReferenceCountMessage? ObjectReferenceCount
        {
            get
            {
                _objectReferenceCount ??= Header
                        .GetMessages<ObjectReferenceCountMessage>()
                        .FirstOrDefault();

                return _objectReferenceCount;
            }
        }

        private protected ObjectHeader Header
        {
            get
            {
                if (_header is null)
                {
                    Context.Reader.Seek((long)Reference.Value, SeekOrigin.Begin);
                    _header = ObjectHeader.Construct(Context);
                }

                return _header;
            }
        }

        #endregion

        #region Methods

        private uint GetReferenceCount()
        {
            var header1 = Header as ObjectHeader1;

            if (header1 is not null)
                return header1.ObjectReferenceCount;

            else
                return ObjectReferenceCount is null
                    ? 1
                    : ObjectReferenceCount.ReferenceCount;
        }

        #endregion
    }
}

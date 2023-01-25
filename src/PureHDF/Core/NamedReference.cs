using System.Runtime.CompilerServices;

namespace PureHDF
{
    internal struct NamedReference
    {
        #region Constructors

        public NamedReference(string name, ulong value, H5File file)
        {
            Name = name;
            Value = value;
            File = file;
            ScratchPad = null;
            Exception = null;
        }

        public NamedReference(string name, ulong value)
        {
            Name = name;
            Value = value;
            File = null;
            ScratchPad = null;
            Exception = null;
        }

        #endregion

        #region Properties

        public string Name { get; set; }

        public ulong Value { get; }

        public H5File? File { get; }

        public ObjectHeaderScratchPad? ScratchPad { get; set; }

        public Exception? Exception { get; set; }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public H5Object Dereference()
        {
            if (File is null)
            {
                return new H5UnresolvedLink(this);
            }
            else if (ScratchPad is not null)
            {
                return new H5Group(File, File.Context, this);
            }
            else
            {
                var context = File.Context;
                context.Reader.Seek((long)Value, SeekOrigin.Begin);
                var objectHeader = ObjectHeader.Construct(context);

                return objectHeader.ObjectType switch
                {
                    ObjectType.Group => new H5Group(File, context, this, objectHeader),
                    ObjectType.Dataset => new H5Dataset(File, context, this, objectHeader),
                    ObjectType.CommitedDatatype => new H5CommitedDatatype(context, this, objectHeader),
                    _ => throw new Exception("Unknown object type.")
                };
            }
        }

        #endregion
    }
}

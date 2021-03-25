using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    internal struct NamedReference
    {
        #region Constructors

        public NamedReference(string name, ulong value, H5File file)
        {
            this.Name = name;
            this.Value = value;
            this.File = file;
            this.ScratchPad = null;
            this.Exception = null;
        }

        public NamedReference(string name, ulong value)
        {
            this.Name = name;
            this.Value = value;
            this.File = null;
            this.ScratchPad = null;
            this.Exception = null;
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
            if (this.File is null)
            {
                return new H5UnresolvedLink(this);
            }
            else if (this.ScratchPad is not null)
            {
                return new H5Group(this.File, this.File.Context, this);
            }
            else
            {
                var context = this.File.Context;
                context.Reader.Seek((long)this.Value, SeekOrigin.Begin);
                var objectHeader = ObjectHeader.Construct(context);

                return objectHeader.ObjectType switch
                {
                    ObjectType.Group => new H5Group(this.File, context, this, objectHeader),
                    ObjectType.Dataset => new H5Dataset(this.File, context, this, objectHeader),
                    ObjectType.CommitedDatatype => new H5CommitedDatatype(context, objectHeader, this),
                    _ => throw new Exception("Unknown object type.")
                };
            }
        }

        #endregion
    }
}

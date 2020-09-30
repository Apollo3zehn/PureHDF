using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public struct H5NamedReference
    {
        #region Constructors

        internal H5NamedReference(H5File file, string name, ulong value)
        {
            this.File = file;
            this.Name = name;
            this.Value = value;
            this.ScratchPad = null;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public ulong Value { get; }

        internal H5File File { get; }

        internal ObjectHeaderScratchPad? ScratchPad { get; set; }


        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal H5Object Dereference()
        {
            if (this.Equals(default(H5NamedReference)))
            {
                return new H5UnresolvedLink(this.File, this.Name);
            }
            else if (this.ScratchPad != null)
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
                    H5ObjectType.Group => new H5Group(this.File, context, this, objectHeader),
                    H5ObjectType.Dataset => new H5Dataset(context, this, objectHeader),
                    H5ObjectType.CommitedDatatype => new H5CommitedDatatype(context, objectHeader, this),
                    _ => throw new Exception("Unknown object type.")
                };
            }
        }

        #endregion
    }
}

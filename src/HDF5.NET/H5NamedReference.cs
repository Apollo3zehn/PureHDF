using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public struct H5NamedReference
    {
        #region Constructors

        internal H5NamedReference(string name, ulong value)
        {
            this.Name = name;
            this.Value = value;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public ulong Value { get; }

        #endregion

        #region Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal H5Object Dereference(H5File file, H5Context context)
        {
            context.Reader.Seek((long)this.Value, SeekOrigin.Begin);
            var objectHeader = ObjectHeader.Construct(context);

            return objectHeader.ObjectType switch
            {
                H5ObjectType.Group => new H5Group(file, context, this, objectHeader),
                H5ObjectType.Dataset => new H5Dataset(context, this, objectHeader),
                H5ObjectType.CommitedDatatype => new H5CommitedDatatype(context, objectHeader, this),
                _ => throw new Exception("Unknown object type.")
            };
        }

        #endregion
    }
}

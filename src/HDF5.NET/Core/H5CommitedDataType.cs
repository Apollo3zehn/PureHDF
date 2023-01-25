using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Datatype.Class}'")]
    partial class H5CommitedDatatype : H5AttributableObject
    {
        #region Constructors

        internal H5CommitedDatatype(H5Context context, NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            Datatype = header.GetMessage<DatatypeMessage>();
        }

        #endregion

        #region Properties

        internal DatatypeMessage Datatype { get; }

        #endregion
    }
}
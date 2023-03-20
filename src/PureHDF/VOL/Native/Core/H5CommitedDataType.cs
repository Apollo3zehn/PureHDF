using System.Diagnostics;

namespace PureHDF.VOL.Native;

[DebuggerDisplay("{Name}: Class = '{Datatype.Class}'")]
internal class H5CommitedDatatype : H5AttributableObject, IH5CommitedDatatype
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
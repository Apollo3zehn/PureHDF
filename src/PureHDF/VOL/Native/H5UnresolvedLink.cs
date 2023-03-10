using System.Diagnostics;

namespace PureHDF;

[DebuggerDisplay("{Name}")]
internal class H5UnresolvedLink : H5Object, IH5UnresolvedLink
{
    #region Constructors

    internal H5UnresolvedLink(NamedReference reference)
        : base(default, reference)
    {
        Reason = reference.Exception;
    }

    #endregion

    #region Properties

    public Exception? Reason { get; }

    #endregion
}
using System.Diagnostics;

namespace PureHDF.VOL.Native;

[DebuggerDisplay("{Name}")]
internal class NativeUnresolvedLink : NativeObject, IH5UnresolvedLink
{
    #region Constructors

    internal NativeUnresolvedLink(NativeNamedReference reference)
        : base(default, reference)
    {
        Reason = reference.Exception;
    }

    #endregion

    #region Properties

    public Exception? Reason { get; }

    #endregion
}
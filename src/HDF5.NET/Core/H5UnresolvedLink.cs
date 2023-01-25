using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    partial class H5UnresolvedLink : H5Object
    {
        #region Constructors

        internal H5UnresolvedLink(NamedReference reference)
            : base(default, reference)
        {
            Reason = reference.Exception;
        }

        #endregion
    }
}

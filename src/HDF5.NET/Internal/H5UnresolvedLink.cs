using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public partial class H5UnresolvedLink : H5Object
    {
        #region Constructors

        internal H5UnresolvedLink(H5NamedReference reference) 
            : base(default, reference)
        {
            this.Reason = reference.Exception;
        }

        #endregion
    }
}

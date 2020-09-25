using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5UnresolvedLink : H5Link
    {
        #region Constructors

        internal H5UnresolvedLink(string name) : base(name)
        {
            //
        }

        #endregion
    }
}

using System;
using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5UnresolvedLink : H5Link
    {
        #region Constructors

        internal H5UnresolvedLink(string name, Exception ex) : base(name)
        {
            this.Reason = ex;
        }

        #endregion

        #region Properties

        public Exception Reason { get; }

        #endregion
    }
}

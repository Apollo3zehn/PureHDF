using System;
using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5UnresolvedLink : H5Object
    {
        #region Constructors

        internal H5UnresolvedLink(string name, Exception ex) 
            : base(default, new H5NamedReference(name, Superblock.UndefinedAddress))
        {
            this.Reason = ex;
        }

        #endregion

        #region Properties

        public Exception Reason { get; }

        #endregion
    }
}

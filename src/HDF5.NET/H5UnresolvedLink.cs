using System;
using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5UnresolvedLink : H5Object
    {
        #region Constructors

        internal H5UnresolvedLink(H5File file, string name) 
            : base(default, new H5NamedReference(file, name, Superblock.UndefinedAddress))
        {
            //
        }

        #endregion
    }
}

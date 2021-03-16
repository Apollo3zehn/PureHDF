using System;

namespace HDF5.NET
{
    public partial class H5UnresolvedLink : H5Object
    {
        #region Properties

        public Exception? Reason { get; }

        #endregion
    }
}

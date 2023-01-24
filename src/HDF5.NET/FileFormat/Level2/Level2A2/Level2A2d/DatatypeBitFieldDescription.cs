using System.Diagnostics;

namespace HDF5.NET
{
    internal abstract class DatatypeBitFieldDescription
    {
        #region Constructors

        public DatatypeBitFieldDescription(H5BaseReader reader)
        {
            Data = reader.ReadBytes(3);
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected byte[] Data { get; }

        #endregion
    }
}

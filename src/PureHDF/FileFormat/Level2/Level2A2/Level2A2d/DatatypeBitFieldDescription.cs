using System.Diagnostics;

namespace PureHDF
{
    internal abstract class DatatypeBitFieldDescription
    {
        #region Constructors

        public DatatypeBitFieldDescription(H5DriverBase driver)
        {
            Data = driver.ReadBytes(3);
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected byte[] Data { get; }

        #endregion
    }
}

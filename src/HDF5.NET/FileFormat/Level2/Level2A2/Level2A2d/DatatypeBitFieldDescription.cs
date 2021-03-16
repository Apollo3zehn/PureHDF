using System.Diagnostics;

namespace HDF5.NET
{
    internal abstract class DatatypeBitFieldDescription : FileBlock
    {
        #region Constructors

        public DatatypeBitFieldDescription(H5BinaryReader reader) : base(reader)
        {
            this.Data = reader.ReadBytes(3);
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected byte[] Data { get; }

        #endregion
    }
}

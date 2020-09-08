using System.Diagnostics;

namespace HDF5.NET
{
    public abstract class FileBlock
    {
        #region Constructors

        public FileBlock(H5BinaryReader reader)
        {
            this.Reader = reader;
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal H5BinaryReader Reader { get; }

        #endregion
    }
}

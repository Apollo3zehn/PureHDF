using System.Diagnostics;

namespace HDF5.NET
{
    internal abstract class FileBlock
    {
        #region Constructors

        public FileBlock(H5BinaryReader reader)
        {
            Reader = reader;
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal H5BinaryReader Reader { get; }

        #endregion
    }
}

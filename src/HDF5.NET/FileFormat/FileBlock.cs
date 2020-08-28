using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace HDF5.NET
{
    public abstract class FileBlock
    {
        #region Constructors

        public FileBlock(BinaryReader reader)
        {
            this.Reader = reader;
        }

        #endregion

        #region Properties

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal BinaryReader Reader { get; }

        #endregion

        #region Methods

        public virtual void Print(ILogger logger)
        {
            logger.LogWarning($"Printing of file block type '{this.GetType()}' is not implemented.");
        }

        #endregion
    }
}

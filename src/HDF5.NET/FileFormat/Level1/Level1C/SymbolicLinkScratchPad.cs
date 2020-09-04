using Microsoft.Extensions.Logging;
using System.IO;

namespace HDF5.NET
{
    public class SymbolicLinkScratchPad : ScratchPad
    {
        #region Constructors

        public SymbolicLinkScratchPad(BinaryReader reader) : base(reader)
        {
            this.LinkValueOffset = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint LinkValueOffset { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class SymbolTableNode
    {
        #region Constructors

        public SymbolTableNode()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ushort SymbolCount { get; set; }
        public List<SymbolTableEntry> GroupEntries { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class VdsGlobalHeapBlock
    {
        #region Constructors

        public VdsGlobalHeapBlock()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ulong EntryCount { get; set; }
        public List<string> SourceFileNames { get; set; }
        public List<string> SourceDatasets { get; set; }
        public List<DataspaceSelection> SourceSelections { get; set; }
        public List<DataspaceSelection> VirtualSelections { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class FractalHeapIndirectBlock
    {
        #region Constructors

        public FractalHeapIndirectBlock()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public List<ulong> ChildDirectBlockAdresses { get; set; }
        public List<ulong> FilteredDirectBlockSizes { get; set; }
        public List<ulong> DirectBlockFilterMask { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}

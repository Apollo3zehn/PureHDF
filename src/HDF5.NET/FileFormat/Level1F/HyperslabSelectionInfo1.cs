using System.Collections.Generic;

namespace HDF5.NET
{
    public abstract class HyperslabSelectionInfo1
    {
        #region Constructors

        public HyperslabSelectionInfo1()
        {
            //
        }

        #endregion

        #region Properties

        public uint Length { get; set; }
        public uint Rank { get; set; }
        public uint BlockCount { get; set; }
        public List<uint> BlockOffsets { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public abstract class HyperslabSelectionInfo2
    {
        #region Constructors

        public HyperslabSelectionInfo2()
        {
            //
        }

        #endregion

        #region Properties

        public byte Flags { get; set; }
        public uint Length { get; set; }
        public uint Rank { get; set; }
        public uint BlockCount { get; set; }
        public List<uint> Starts { get; set; }
        public List<uint> Strides { get; set; }
        public List<uint> Counts { get; set; }
        public List<uint> Blocks { get; set; }

        #endregion
    }
}

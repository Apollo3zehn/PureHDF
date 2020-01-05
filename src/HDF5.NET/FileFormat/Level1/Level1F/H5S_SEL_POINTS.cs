using System.Collections.Generic;

namespace HDF5.NET
{
    public class H5S_SEL_POINTS : H5S_SEL
    {
        #region Constructors

        public H5S_SEL_POINTS()
        {
            //
        }

        #endregion

        #region Properties

        public uint Version { get; set; }
        public uint Length { get; set; }
        public uint Rank { get; set; }
        public uint PointCount { get; set; }
        public List<uint> PointData { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class DataspaceMessage
    {
#warning remember to parse different versions correctly
        #region Constructors

        public DataspaceMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public byte Dimensionality { get; set; }
        public DataspaceMessageFlags Flags { get; set; }
        public DataspaceMessageType Type { get; set; }
        public List<ulong> DimensionSizes { get; set; }
        public List<ulong> DimensionMaxSizes { get; set; }
        public List<ulong> PermutationIndices { get; set; }

        #endregion
    }
}

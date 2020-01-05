using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class FilterPipelineMessage : Message
    {
        #region Constructors

        public FilterPipelineMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public byte FilterCount { get; set; }
        public List<FilterDescription> FilterDescriptions { get; set; }

        #endregion
    }
}

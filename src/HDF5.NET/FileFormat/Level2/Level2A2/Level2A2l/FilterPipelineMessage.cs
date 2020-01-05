using System.Collections.Generic;

namespace HDF5.NET
{
    public class FilterPipelineMessage
    {
        #region Constructors

        public FilterPipelineMessage()
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

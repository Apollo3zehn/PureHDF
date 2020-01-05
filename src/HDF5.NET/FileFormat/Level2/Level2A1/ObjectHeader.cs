using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public class ObjectHeader : FileBlock
    {
        #region Constructors

        public ObjectHeader(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public List<HeaderMessage> HeaderMessages { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class BTree2Record10
    {
        #region Constructors

        public BTree2Record10()
        {
            //
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public List<ulong> ScaledOffsets { get; set; }

        #endregion
    }
}

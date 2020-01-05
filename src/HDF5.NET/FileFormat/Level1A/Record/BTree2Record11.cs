using System.Collections.Generic;

namespace HDF5.NET
{
    public class BTree2Record11
    {
        #region Constructors

        public BTree2Record11()
        {
            //
        }

        #endregion

        #region Properties

        public ulong Address { get; set; }
        public ulong ChunkSize { get; set; }
        public uint FilterMask { get; set; }
        public List<ulong> ScaledOffsets { get; set; }

        #endregion
    }
}

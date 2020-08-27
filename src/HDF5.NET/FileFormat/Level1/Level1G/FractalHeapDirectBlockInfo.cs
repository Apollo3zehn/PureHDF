using System;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public struct FractalHeapDirectBlockInfo
    {
        #region Properties

        public ulong Address { get; set; }
        public ulong FilteredSize { get; set; }
        public uint FilterMask { get; set; }

        #endregion
    }
}

using System.Collections.Generic;

namespace HDF5.NET
{
    public class SharedObjectHeaderMessageTable
    {
        #region Constructors

        public SharedObjectHeaderMessageTable()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public List<byte> Versions { get; set; }
        public List<MessageTypeFlags> MessageTypeFlags { get; set; }
        public List<uint> MinimumMessageSize { get; set; }
        public List<ushort> ListCutoff { get; set; }
        public List<ushort> BTree2Cutoff { get; set; }
        public List<ushort> MessageCount { get; set; }
        public List<ulong> IndexAddress { get; set; }
        public List<ulong> FractalHeapAddress { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}

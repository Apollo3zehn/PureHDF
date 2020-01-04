using System.Collections.Generic;

namespace HDF5.NET
{
    public class BTree2InternalNode
    {
        #region Constructors

        public BTree2InternalNode()
        {
            //
        }

        #endregion

        #region Properties

        public byte[] Signature { get; set; }
        public byte Version { get; set; }
        public BTree2Type Type { get; set; }
        public List<byte[]> Records { get; set; }
        public List<ulong> ChildNodePointers { get; set; }
        // other data
        public uint Checksum { get; set; }

        #endregion
    }
}

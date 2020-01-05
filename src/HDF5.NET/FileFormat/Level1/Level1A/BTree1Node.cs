using System.Collections.Generic;

namespace HDF5.NET
{
    public class BTree1Node
    {
        #region Constructors

        public BTree1Node()
        {
            //
        }

        #endregion

        #region Properties

        public char[] Signature { get; set; }
        public BTree1NodeType NodeType { get; set; }
        public byte NodeLevel { get; set; }
        public ushort EntriesUsed { get; set; }
        public ulong LeftAddress { get; set; }
        public ulong RightAddress { get; set; }
        public List<BTree1Key> Keys { get; set; }
        public List<ulong> ChildAddresses { get; set; }

        #endregion
    }
}

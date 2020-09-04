using Microsoft.Extensions.Logging;
using System.IO;

namespace HDF5.NET
{
    public class BTree1GroupKey : BTree1Key
    {
        #region Constructors

        public BTree1GroupKey(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.LocalHeapByteOffset = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong LocalHeapByteOffset { get; set; }

        #endregion
    }
}

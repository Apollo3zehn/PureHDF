using System;
using System.IO;

namespace HDF5.NET
{
    internal class SymbolTableMessage : Message
    {
        #region Fields

#warning Is this OK?
        Superblock _superblock;

        #endregion

        #region Constructors

        public SymbolTableMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            BTree1Address = superblock.ReadOffset(reader);
            LocalHeapAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong LocalHeapAddress { get; set; }

        public LocalHeap LocalHeap
        {
            get
            {
                Reader.Seek((long)LocalHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(Reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
        {
            Reader.Seek((long)BTree1Address, SeekOrigin.Begin);
            return new BTree1Node<BTree1GroupKey>(Reader, _superblock, decodeKey);
        }

        #endregion
    }
}

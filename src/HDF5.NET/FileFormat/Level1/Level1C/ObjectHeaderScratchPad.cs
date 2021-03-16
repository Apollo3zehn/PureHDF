using System;
using System.IO;

namespace HDF5.NET
{
    internal class ObjectHeaderScratchPad : ScratchPad
    {
        #region Fields

        private Superblock _superblock;

        #endregion

        #region Constructors

        public ObjectHeaderScratchPad(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            this.BTree1Address = superblock.ReadLength(reader);
            this.NameHeapAddress = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong NameHeapAddress { get; set; }

        public LocalHeap LocalHeap
        {
            get
            {
                this.Reader.Seek((long)this.NameHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(this.Reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public BTree1Node<BTree1GroupKey> GetBTree1(Func<BTree1GroupKey> decodeKey)
        {
            this.Reader.Seek((long)this.BTree1Address, SeekOrigin.Begin);
            return new BTree1Node<BTree1GroupKey>(this.Reader, _superblock, decodeKey);
        }

        #endregion
    }
}

using System.IO;

namespace HDF5.NET
{
    public class SymbolTableMessage : Message
    {
        #region Fields

#warning Is this OK?
        Superblock _superblock;

        #endregion

        #region Constructors

        public SymbolTableMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            this.BTree1Address = superblock.ReadOffset(reader);
            this.LocalHeapAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong LocalHeapAddress { get; set; }

        public BTree1Node<BTree1GroupKey> BTree1
        {
            get
            {
                this.Reader.Seek((long)this.BTree1Address, SeekOrigin.Begin);
                return new BTree1Node<BTree1GroupKey>(this.Reader, _superblock);
            }
        }

        public LocalHeap LocalHeap
        {
            get
            {
                this.Reader.Seek((long)this.LocalHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(this.Reader, _superblock);
            }
        }

        #endregion
    }
}

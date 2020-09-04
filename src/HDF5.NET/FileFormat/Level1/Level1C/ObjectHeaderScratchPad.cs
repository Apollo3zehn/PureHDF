using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Security;

namespace HDF5.NET
{
    public class ObjectHeaderScratchPad : ScratchPad
    {
        #region Fields

        private Superblock _superblock;

        #endregion

        #region Constructors

        public ObjectHeaderScratchPad(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            this.BTreeAddress = superblock.ReadLength(reader);
            this.NameHeapAddress = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong BTreeAddress { get; set; }
        public ulong NameHeapAddress { get; set; }

        public BTree1Node BTree1
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.BTreeAddress, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        public LocalHeap LocalHeap
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.NameHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(this.Reader, _superblock);
            }
        }

        #endregion
    }
}

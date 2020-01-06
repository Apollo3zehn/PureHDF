using Microsoft.Extensions.Logging;
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

        public SymbolTableMessage(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            this.BTree1Address = superblock.ReadOffset();
            this.LocalHeapAddress = superblock.ReadOffset();
        }

        #endregion

        #region Properties

        public ulong BTree1Address { get; set; }
        public ulong LocalHeapAddress { get; set; }

        public BTree1Node BTree1
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.BTree1Address, SeekOrigin.Begin);
                return new BTree1Node(this.Reader, _superblock);
            }
        }

        public LocalHeap LocalHeap
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.LocalHeapAddress, SeekOrigin.Begin);
                return new LocalHeap(this.Reader, _superblock);
            }
        }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation("SymbolTableMessage");

            base.Print(logger);

            logger.LogInformation("SymbolTableMessage BTree1");
            this.BTree1.Print(logger);
        }

        #endregion
    }
}

using Microsoft.Extensions.Logging;
using System.IO;

namespace HDF5.NET
{
    public class ObjectHeaderScratchPad : ScratchPad
    {
        #region Constructors

        public ObjectHeaderScratchPad(BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.BTreeAddress = superblock.ReadLength(reader);
            this.NameHeapAddress = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong BTreeAddress { get; set; }
        public ulong NameHeapAddress { get; set; }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"ObjectHeaderScratchPad");
            logger.LogInformation($"ObjectHeaderScratchPad BTreeAddress: {this.BTreeAddress}");
            logger.LogInformation($"ObjectHeaderScratchPad NameHeapAddress: {this.NameHeapAddress}");
        }

        #endregion
    }
}

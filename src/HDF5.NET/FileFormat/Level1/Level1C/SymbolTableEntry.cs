using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class SymbolTableEntry : FileBlock
    {
        #region Fields

#warning OK like this?
        Superblock _superblock;

        #endregion

        #region Constructors

        public SymbolTableEntry(BinaryReader reader, Superblock superblock) : base(reader)
        {
            _superblock = superblock;

            // link name offset
            this.LinkNameOffset = superblock.ReadOffset();
            
            // object header address
            this.ObjectHeaderAddress = superblock.ReadOffset();

            // cache type
            this.CacheType = (CacheType)reader.ReadUInt32();

            // reserved
            reader.ReadUInt32();

            // scratch pad
            var before = reader.BaseStream.Position;

            this.ScratchPad = this.CacheType switch
            {
                CacheType.NoCache => null,
                CacheType.ObjectHeader => new ObjectHeaderScratchPad(reader, superblock),
                CacheType.SymbolicLink => new SymbolicLinkScratchPad(reader),
                _ => throw new NotSupportedException()
            };

            var after = reader.BaseStream.Position;
            var length = after - before;

            // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
            reader.ReadBytes((int)(16 - length));
        }

        #endregion

        #region Properties

        public ulong LinkNameOffset { get; set; }
        public ulong ObjectHeaderAddress { get; set; }
        public CacheType CacheType { get; set; }
        public ScratchPad? ScratchPad { get; set; }

        public ObjectHeader ObjectHeader
        {
            get
            {
                this.Reader.BaseStream.Seek((long)this.ObjectHeaderAddress, SeekOrigin.Begin);
                return ObjectHeader.Construct(this.Reader, _superblock);
            }
        }

        public string Name { get; set; }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"SymbolTableEntry");
            logger.LogInformation($"SymbolTableEntry Cache Type: {this.CacheType}");
            logger.LogInformation($"SymbolTableEntry LinkNameOffset: {this.LinkNameOffset}");

            if (this.ScratchPad != null)
            {
                logger.LogInformation($"SymbolTableEntry ScratchPad");
                this.ScratchPad.Print(logger);
            }

            logger.LogInformation($"SymbolTableEntry ObjectHeader");
            this.ObjectHeader.Print(logger);
        }

        #endregion
    }
}

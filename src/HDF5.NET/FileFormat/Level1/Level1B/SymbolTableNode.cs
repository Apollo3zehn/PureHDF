using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class SymbolTableNode : FileBlock
    {
        #region Constructors

        public SymbolTableNode(BinaryReader reader, Superblock superblock) : base(reader)
        {
            var signature = reader.ReadBytes(4);
            this.ValidateSignature(signature, SymbolTableNode.Signature);

            this.Version = reader.ReadByte();
            reader.ReadByte();
            this.SymbolCount = reader.ReadUInt16();

            this.GroupEntries = new List<SymbolTableEntry>();

            for (int i = 0; i < this.SymbolCount; i++)
            {
                this.GroupEntries.Add(new SymbolTableEntry(reader, superblock));
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("SNOD");

        public byte Version { get; set; }
        public ushort SymbolCount { get; set; }
        public List<SymbolTableEntry> GroupEntries { get; set; }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"SymbolTableNode");

            base.Print(logger);

            for (int i = 0; i < this.SymbolCount; i++)
            {
                logger.LogInformation($"SymbolTableNode Entry[{i}]");
                this.GroupEntries[i].Print(logger);
            }
        }

        #endregion
    }
}

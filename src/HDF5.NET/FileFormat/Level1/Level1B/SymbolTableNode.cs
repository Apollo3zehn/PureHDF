using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class SymbolTableNode : FileBlock
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SymbolTableNode(BinaryReader reader, Superblock superblock) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, SymbolTableNode.Signature);

            // version
            this.Version = reader.ReadByte();

            // reserved
            reader.ReadByte();

            // symbol count
            this.SymbolCount = reader.ReadUInt16();

            // group entries
            this.GroupEntries = new List<SymbolTableEntry>();

            for (int i = 0; i < this.SymbolCount; i++)
            {
                this.GroupEntries.Add(new SymbolTableEntry(reader, superblock));
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("SNOD");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(SymbolTableNode)} are supported.");

                _version = value;
            }
        }

        public ushort SymbolCount { get; set; }
        public List<SymbolTableEntry> GroupEntries { get; set; }

        #endregion

        #region Methods

        public void AssignNames(LocalHeap heap)
        {
            foreach (var entry in this.GroupEntries)
            {
                entry.Name = heap.GetObjectName(entry.LinkNameOffset);
            }
        }
         
        public void GetLinks(LocalHeap heap)
        {

        }

        public override void Print(ILogger logger)
        {
            logger.LogInformation($"SymbolTableNode");

            for (int i = 0; i < this.SymbolCount; i++)
            {
                logger.LogInformation($"SymbolTableNode Entry[{i}]");
                this.GroupEntries[i].Print(logger);
            }
        }

        #endregion
    }
}

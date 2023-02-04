﻿using System.Text;

namespace PureHDF
{
    internal class SymbolTableNode
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public SymbolTableNode(H5Context context)
        {
            var reader = context.Reader;

            // signature
            var signature = reader.ReadBytes(4);
            Utils.ValidateSignature(signature, SymbolTableNode.Signature);

            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadByte();

            // symbol count
            SymbolCount = reader.ReadUInt16();

            // group entries
            GroupEntries = new List<SymbolTableEntry>();

            for (int i = 0; i < SymbolCount; i++)
            {
                GroupEntries.Add(new SymbolTableEntry(context));
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
    }
}

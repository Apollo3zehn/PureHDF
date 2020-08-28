using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class BTree2LeafNode : BTree2Node
    {
        #region Fields

        private byte _version;

        #endregion

        #region Constructors

        public BTree2LeafNode(BinaryReader reader, Superblock superblock, BTree2Type type, ushort recordSize, ulong recordCount) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree2LeafNode.Signature);

            // version
            this.Version = reader.ReadByte();

            // type
            this.Type = (BTree2Type)reader.ReadByte();

            if (this.Type != type)
                throw new FormatException($"The BTree2 leaf node type ('{this.Type}') does not match the type defined in the header ('{type}').");

            // records
            this.Records = new List<BTree2Record>((int)recordCount);

            for (ulong i = 0; i < recordCount; i++)
            {
                var record = (BTree2Record)(this.Type switch
                {
                    BTree2Type.IndexingIndirectlyAccessed_NonFilteredHugeFractalHeapObjects => new BTree2Record01(reader, superblock),
                    BTree2Type.IndexingIndirectlyAccessed_FilteredHugeFractalHeapObjects => new BTree2Record02(reader, superblock),
                    BTree2Type.IndexingDirectlyAccessed_NonFilteredHugeFractalHeapObjects => new BTree2Record03(reader, superblock),
                    BTree2Type.IndexingDirectlyAccessed_FilteredHugeFractalHeapObjects => new BTree2Record04(reader, superblock),
                    BTree2Type.IndexingNameField_Links => new BTree2Record05(reader),
                    BTree2Type.IndexingCreationOrderField_Links => new BTree2Record06(reader),
                    BTree2Type.IndexingSharedObjectHeaderMessages => BTree2Record07.Construct(reader, superblock),
                    BTree2Type.IndexingNameField_Attributes => new BTree2Record08(reader),
                    BTree2Type.IndexingCreationOrderField_Attributes => new BTree2Record09(reader),
                    BTree2Type.IndexingChunksOfDatasets_WithoutFilters_WithMoreThanOneUnlimDim => new BTree2Record10(reader, superblock, recordSize),
                    BTree2Type.IndexingChunksOfDatasets_WithFilters_WithMoreThanOneUnlimDim => new BTree2Record11(reader, superblock, recordSize),
                    BTree2Type.Testing => throw new Exception($"Record type {nameof(BTree2Type.Testing)} should only be used for testing."),
                    _ => throw new Exception($"Unknown record type '{this.Type}'.")
                });

                this.Records.Add(record);
            }

            // checksum
            this.Checksum = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("BTLF");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(BTree2LeafNode)} are supported.");

                _version = value;
            }
        }

        public BTree2Type Type { get; set; }
        public List<BTree2Record> Records { get; set; }
        public uint Checksum { get; set; }

        #endregion
    }
}

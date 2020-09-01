using System;
using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public abstract class BTree2Node : FileBlock
    {
        #region Fields

        private byte _version;
        private byte[] _signature;

        #endregion

        public BTree2Node(BinaryReader reader, Superblock superblock, BTree2Header header, ushort recordCount, byte[] signature) 
            : base(reader)
        {
            _signature = signature;

            // signature
            var actualSignature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(actualSignature, BTree2InternalNode.Signature);

            // version
            this.Version = reader.ReadByte();

            // type
            this.Type = (BTree2Type)reader.ReadByte();

            if (this.Type != header.Type)
                throw new FormatException($"The BTree2 internal node type ('{this.Type}') does not match the type defined in the header ('{header.Type}').");

            // records
            this.Records = new List<BTree2Record>(recordCount);

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
                    BTree2Type.IndexingChunksOfDatasets_WithoutFilters_WithMoreThanOneUnlimDim => new BTree2Record10(reader, superblock, header.RecordSize),
                    BTree2Type.IndexingChunksOfDatasets_WithFilters_WithMoreThanOneUnlimDim => new BTree2Record11(reader, superblock, header.RecordSize),
                    BTree2Type.Testing => throw new Exception($"Record type {nameof(BTree2Type.Testing)} should only be used for testing."),
                    _ => throw new Exception($"Unknown record type '{this.Type}'.")
                });

                this.Records.Add(record);
            }
        }

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(BTree2Node)} are supported.");

                _version = value;
            }
        }

        public BTree2Type Type { get; }
        public List<BTree2Record> Records { get; }

        public uint Checksum { get; protected set; }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public abstract class H5Link
    {
        #region Fields

        private IEnumerable<H5Attribute> _attributes;

        #endregion

        #region Constructors

        internal H5Link(NamedObject namedObject, Superblock superblock)
        {
            this.Name = namedObject.Name;
            this.ObjectHeader = namedObject.Header;
            this.Superblock = superblock;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public ObjectHeader ObjectHeader { get; }

        public Superblock Superblock { get; }

        public IEnumerable<H5Attribute> Attributes
        {
            get
            {
                if (_attributes == null)
                    _attributes = this.GetAttributes();

                return _attributes;
            }
        }

        #endregion

        #region Methods

        private IEnumerable<H5Attribute> GetAttributes()
        {

            // get attributes from attribute message
            var attributeMessages = this.ObjectHeader.GetMessages<AttributeMessage>();

            foreach (var message in attributeMessages)
            {
                var attribute = new H5Attribute(message, this.Superblock);
                yield return attribute;
            }

            // get attributes from attribute info
            var infoMessages = this.ObjectHeader.GetMessages<AttributeInfoMessage>();

            foreach (var infoMessage in infoMessages)
            {
                var attributes = this.GetAttributesFromAttributeInfo(infoMessage);

                foreach (var attribute in attributes)
                {
                    yield return attribute;
                }
            }
        }

        private IEnumerable<H5Attribute> GetAttributesFromAttributeInfo(AttributeInfoMessage infoMessage)
        {
            // fractal heap header
            var reader = this.Superblock.Reader;
            reader.BaseStream.Seek((long)infoMessage.FractalHeapAddress, SeekOrigin.Begin);
            var heapHeader = new FractalHeapHeader(reader, this.Superblock);

            // b-tree v2
            reader.BaseStream.Seek((long)infoMessage.BTree2NameIndexAddress, SeekOrigin.Begin);
            var btree2 = new BTree2Header<BTree2Record08>(reader, this.Superblock);

            var records = btree2
                .GetRecords()
                .ToList();

            // local cache: indirectly accessed, non-filtered
            IEnumerable<BTree2Record01>? record01Cache = null;

            foreach (var record in records)
            {
                using var localReader = new BinaryReader(new MemoryStream(record.HeapId));
                var heapId = FractalHeapId.Construct(localReader, this.Superblock, heapHeader);

                yield return heapId switch
                {
                    // managed
                    ManagedObjectsFractalHeapId managed => this.ReadManagedAttribute(reader, heapHeader, managed),

                    // indirectly accessed, filtered (must be listed before 'huge1' to make switch expression work)
                    HugeObjectsFractalHeapIdSubType2 huge2 => throw new Exception("Filtered attributes are not supported."),

                    // indirectly accessed, non-filtered
                    HugeObjectsFractalHeapIdSubType1 huge1 => this.ReadHugeAttribute1(reader, heapHeader, huge1, ref record01Cache),

                    // directly accessed, non-filtered
                    HugeObjectsFractalHeapIdSubType3 huge3 => this.ReadHugeAttribute3(reader, huge3),

                    // directly accessed, filtered
                    HugeObjectsFractalHeapIdSubType4 huge4 => throw new Exception("Filtered attributes are not supported."),

                    // tiny (extended) (must be listed before 'tiny1' to make switch expression work)
                    TinyObjectsFractalHeapIdSubType2 tiny2 => this.ReadTinyAttribute(tiny2),

                    // tiny (normal)
                    TinyObjectsFractalHeapIdSubType1 tiny1 => this.ReadTinyAttribute(tiny1),

                    // default
                    _  => throw new Exception($"Fractal heap ID type '{heapId.GetType().Name}' is not supported.")
                };
            }
        }

        private H5Attribute ReadManagedAttribute(BinaryReader reader,
                                                 FractalHeapHeader heapHeader,
                                                 ManagedObjectsFractalHeapId heapId)
        {
            var address = heapHeader.GetAddress(heapId);

            reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
            var message = new AttributeMessage(reader, this.Superblock);
            var attribute = new H5Attribute(message, this.Superblock);

            return attribute;
        }

        private H5Attribute ReadHugeAttribute1(BinaryReader reader,
                                               FractalHeapHeader heapHeader,
                                               HugeObjectsFractalHeapIdSubType1 heapId,
                                               [AllowNull]ref IEnumerable<BTree2Record01> record01Cache)
        {
            // huge objects b-tree v2
            if (record01Cache == null)
            {
                reader.BaseStream.Seek((long)heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
                var hugeBtree2 = new BTree2Header<BTree2Record01>(reader, this.Superblock);
                record01Cache = hugeBtree2.GetRecords();
            }

            var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == heapId.BTree2Key);
            reader.BaseStream.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);
            var message = new AttributeMessage(reader, this.Superblock);
            var attribute = new H5Attribute(message, this.Superblock);

            return attribute;
        }

        private H5Attribute ReadHugeAttribute3(BinaryReader reader,
                                               HugeObjectsFractalHeapIdSubType3 heapId)
        {
            reader.BaseStream.Seek((long)heapId.Address, SeekOrigin.Begin);
            var message = new AttributeMessage(reader, this.Superblock);
            var attribute = new H5Attribute(message, this.Superblock);

            return attribute;
        }

        private H5Attribute ReadTinyAttribute(TinyObjectsFractalHeapIdSubType1 heapId)
        {
            using var localReader = new BinaryReader(new MemoryStream(heapId.Data));
            var message = new AttributeMessage(localReader, this.Superblock);
            var attribute = new H5Attribute(message, this.Superblock);

            return attribute;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public abstract class H5Link
    {
        #region Fields

        private List<H5Attribute> _attributes;

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

        public List<H5Attribute> Attributes
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

        private List<H5Attribute> GetAttributes()
        {
            var attributeMessages = this.ObjectHeader.GetMessages<AttributeMessage>();
            var attributeInfoMessages = this.ObjectHeader.GetMessages<AttributeInfoMessage>();

            var attributes = this.ObjectHeader
                .GetMessages<AttributeMessage>()
                .Select(message => new H5Attribute(message, this.Superblock));

            var moreAttributes = attributeInfoMessages
                .SelectMany(message => this.GetAttributesFromAttributeInfo(message));

            attributes = attributes
                .Concat(moreAttributes);

            return attributes.ToList();
        }

        private IEnumerable<H5Attribute> GetAttributesFromAttributeInfo(AttributeInfoMessage infoMessage)
        {
            // fractal heap header
            var reader = this.Superblock.Reader;
            reader.BaseStream.Seek((long)infoMessage.FractalHeapAddress, SeekOrigin.Begin);
            var heapHeader = new FractalHeapHeader(reader, this.Superblock);

            // find managed attributes
            if (heapHeader.HeapManagedObjectsCount > 0)
            {
                // b-tree v2
                reader.BaseStream.Seek((long)infoMessage.BTree2NameIndexAddress, SeekOrigin.Begin);
                var btree2 = new BTree2Header<BTree2Record08>(reader, this.Superblock);

                var records = btree2
                    .GetRecords()
                    .ToList();

                var heapIds = records.Select(record =>
                {
                    using (var localReader = new BinaryReader(new MemoryStream(record.HeapId)))
                    {
                        return FractalHeapId.Construct(localReader, this.Superblock, heapHeader);
                    };
                }).ToList();

                if (!this.Superblock.IsUndefinedAddress(heapHeader.RootBlockAddress))
                {
                    foreach (var heapId in heapIds)
                    {
                        var address = heapHeader.GetAddress((ManagedObjectsFractalHeapId)heapId);

                        reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
                        var message = new AttributeMessage(reader, this.Superblock);
                        var attribute = new H5Attribute(message, this.Superblock);

                        yield return attribute;
                    }
                }
            }

            // find huge attributes
            if (heapHeader.HeapHugeObjectsCount > 0)
            {
                reader.BaseStream.Seek((long)heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);

                // indirectly accessed, non-filtered
                if (heapHeader.IOFilterEncodedLength == 0 && !heapHeader.HugeIdsAreDirect)
                {
                    var hugeBtree2 = new BTree2Header<BTree2Record01>(reader, this.Superblock);
                    var hugeRecords = hugeBtree2.GetRecords();

                    foreach (var hugeRecord in hugeRecords)
                    {
                        reader.BaseStream.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);
                        var message = new AttributeMessage(reader, this.Superblock);
                        var attribute = new H5Attribute(message, this.Superblock);

                        yield return attribute;
                    }
                }
                // indirectly accessed, filtered
                else if (heapHeader.IOFilterEncodedLength < 0 && !heapHeader.HugeIdsAreDirect)
                {
                    var hugeBtree2 = new BTree2Header<BTree2Record02>(reader, this.Superblock);
                    var hugeRecords = hugeBtree2.GetRecords();

                    foreach (var hugeRecord in hugeRecords)
                    {
                        throw new Exception("Filtered attributes are not supported.");
                    }
                }
                // directly accessed, non-filtered
                else if (heapHeader.IOFilterEncodedLength == 0 && heapHeader.HugeIdsAreDirect)
                {
                    var hugeBtree2 = new BTree2Header<BTree2Record03>(reader, this.Superblock);
                    var hugeRecords = hugeBtree2.GetRecords();

                    foreach (var hugeRecord in hugeRecords)
                    {
                        throw new Exception("Where is the difference to indirectly accessed objects?");
                    }
                }
                // directly accessed, filtered
                else if (heapHeader.IOFilterEncodedLength < 0 && heapHeader.HugeIdsAreDirect)
                {
                    var hugeBtree2 = new BTree2Header<BTree2Record04>(reader, this.Superblock);
                    var hugeRecords = hugeBtree2.GetRecords();

                    foreach (var hugeRecord in hugeRecords)
                    {
                        throw new Exception("Filtered attributes are not supported.");
                    }
                }
            }
        }

        #endregion
    }
}

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

            attributes = attributes.Concat(moreAttributes);
            return attributes.ToList();
        }

        private List<H5Attribute> GetAttributesFromAttributeInfo(AttributeInfoMessage infoMessage)
        {
            var attributes = new List<H5Attribute>();

            // fractal heap header
            var reader = this.Superblock.Reader;
            reader.BaseStream.Seek((long)infoMessage.FractalHeapAddress, SeekOrigin.Begin);
            var header = new FractalHeapHeader(reader, this.Superblock);

            // b-tree v2
            reader.BaseStream.Seek((long)infoMessage.BTree2NameIndexAddress, SeekOrigin.Begin);
            var btree2 = new BTree2Header(reader, this.Superblock);
            var rootNode = btree2.RootNode;

            var a = ((BTree2InternalNode)rootNode).Records
                .Cast<BTree2Record08>()
                .ToList();

            var records = ((BTree2LeafNode)rootNode).Records
                .Cast<BTree2Record08>()
                .ToList();

            var offsetByteCount = (ulong)Math.Ceiling(header.MaximumHeapSize / 8.0);
#warning Is -1 correct?
            var lengthByteCount = H5Utils.FindMinByteCount(header.MaximumDirectBlockSize - 1); 

            var heapIds = records.Select(record =>
            {
                using (var localReader = new BinaryReader(new MemoryStream(record.HeapId)))
                {
                    return FractalHeapId.Construct(localReader, this.Superblock, offsetByteCount, lengthByteCount);
                };
            }).ToList();

            // fractal heap
            if (!this.Superblock.IsUndefinedAddress(header.RootBlockAddress))
            {
                foreach (var heapId in heapIds)
                {
                    var address = header.GetAddress((ManagedObjectsFractalHeapId)heapId);

                    reader.BaseStream.Seek((long)address, SeekOrigin.Begin);
                    var message = new AttributeMessage(reader, this.Superblock);
                    var attribute = new H5Attribute(message, this.Superblock);
                    attributes.Add(attribute);
                }
            }

            return attributes;
        }

        #endregion
    }
}

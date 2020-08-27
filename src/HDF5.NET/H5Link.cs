using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

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

        private IEnumerable<H5Attribute> GetAttributesFromAttributeInfo(AttributeInfoMessage message)
        {
            var reader = this.Superblock.Reader;
            reader.BaseStream.Seek((long)message.FractalHeapAddress, SeekOrigin.Begin);

            // b-tree v2
            reader.BaseStream.Seek((long)message.BTree2NameIndexAddress, SeekOrigin.Begin);
            var btree2 = new BTree2Header(reader, this.Superblock);
            var rootNode = btree2.RootNode;

            // fractal heap
            var header = new FractalHeapHeader(reader, this.Superblock);

            if (!this.Superblock.IsUndefinedAddress(header.RootBlockAddress))
            {
                var isDirectBlock = header.RootIndirectBlockRowsCount == 0;
                reader.BaseStream.Seek((long)header.RootBlockAddress, SeekOrigin.Begin);

                if (isDirectBlock)
                {
                    var directBlock = new FractalHeapDirectBlock(header, reader, this.Superblock);
                }
                else
                {
                    var indirectBlock = new FractalHeapIndirectBlock(header, reader, this.Superblock);

                    foreach (var directBlockInfo in indirectBlock.DirectBlockInfos)
                    {
                        reader.BaseStream.Seek((long)directBlockInfo.Address, SeekOrigin.Begin);
                        var directBlock = new FractalHeapDirectBlock(header, reader, this.Superblock);

#warning Check this.
//#error: superblock.ReadLength() pass always a reader!!
                        var remainingBytes = 1002;//(long)directBlock.ObjectData.Length;
                        var messages = new List<AttributeMessage>();

                        while (remainingBytes > 0)
                        {
                            //var before = localReader.BaseStream.Position;
                            var attributeMessage = new AttributeMessage(reader, this.Superblock);
                            //var after = localReader.BaseStream.Position;
                            messages.Add(attributeMessage);

                            //remainingBytes -= (after - before);
                        }

                        var a = 1;
                    }
                }
            }

            throw new NotFiniteNumberException();
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class H5AttributableLink : H5Link
    {
        #region Fields

        private ulong _objectHeaderAddress;
        private ObjectHeader? _objectHeader;

        #endregion

        #region Constructors

        // only for H5File constructor
        public H5AttributableLink(ObjectHeader objectHeader) : base("/")
        {
            var file = this as H5File;

            if (file == null)
                throw new Exception($"This constructor is only intended for the {nameof(H5File)} class.");

            this.File = file;

            _objectHeader = objectHeader;
        }

        public H5AttributableLink(H5File file, string name, ObjectHeader objectHeader) : base(name)
        {
            this.File = file;
            _objectHeader = objectHeader;
        }

        public H5AttributableLink(H5File file, string name, ulong objectHeaderAddress) : base(name)
        {
            this.File = file;
            _objectHeaderAddress = objectHeaderAddress;
        }

        #endregion

        #region Properties

        public H5File File { get; }

        protected ObjectHeader ObjectHeader
        {
            get
            {
                if (_objectHeader == null)
                {
                    this.File.Reader.Seek((long)_objectHeaderAddress, SeekOrigin.Begin);
                    _objectHeader = ObjectHeader.Construct(this.File.Reader, this.File.Superblock);
                }

                return _objectHeader;
            }
        }

        public IEnumerable<H5Attribute> Attributes => this.GetAttributes();

        #endregion

        #region Methods

        private IEnumerable<H5Attribute> GetAttributes()
        {
            // get attributes from attribute message
            var attributeMessages = this.ObjectHeader.GetMessages<AttributeMessage>();

            foreach (var message in attributeMessages)
            {
                var attribute = new H5Attribute(message, this.File.Superblock);
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
            var fractalHeap = infoMessage.FractalHeap;
            var btree2NameIndex = infoMessage.BTree2NameIndex;
            var records = btree2NameIndex
                .GetRecords()
                .ToList();

            // local cache: indirectly accessed, non-filtered
            IEnumerable<BTree2Record01>? record01Cache = null;

            foreach (var record in records)
            {
                using var localReader = new H5BinaryReader(new MemoryStream(record.HeapId));
                var heapId = FractalHeapId.Construct(this.File.Reader, this.File.Superblock, localReader, fractalHeap);

                yield return heapId.Read(reader =>
                {
                    var message = new AttributeMessage(reader, this.File.Superblock);
                    return new H5Attribute(message, this.File.Superblock);
                }, ref record01Cache);
            }
        }

        #endregion
    }
}

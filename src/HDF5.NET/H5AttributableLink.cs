using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public IEnumerable<H5Attribute> Attributes => this.EnumerateAttributes();

        #endregion

        #region Methods

        public bool AttributeExists(string name)
        {
            return this.TryGetAttributeMessage(name, out var _);
        }

        public H5Attribute GetAttribute(string name)
        {
            if (!this.TryGetAttributeMessage(name, out var attributeMessage))
                throw new Exception($"Could not find attribute '{name}'.");

            return new H5Attribute(attributeMessage, this.File.Superblock);
        }

        private bool TryGetAttributeMessage(string name, [NotNullWhen(returnValue: true)] out AttributeMessage? attributeMessage)
        {
            // get attribute from attribute message
            attributeMessage = this.ObjectHeader
                .GetMessages<AttributeMessage>()
                .FirstOrDefault(message => message.Name == name);

            if (attributeMessage != null)
            {
                return true;
            }
            // get attribute from attribute info
            else
            {
                var attributeInfoMessages = this.ObjectHeader.GetMessages<AttributeInfoMessage>();

                if (attributeInfoMessages.Any())
                {
                    if (attributeInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single attribute info message.");

                    var attributeInfoMessage = attributeInfoMessages.First();

                    if (!this.File.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
                    {
                        if (this.TryGetAttributeMessageFromAttributeInfoMessage(attributeInfoMessage, name, out attributeMessage))
                            return true;
                    }
                }
            }

            return false;
        }

        private IEnumerable<H5Attribute> EnumerateAttributes()
        {
            // AttributeInfoMessage is optional
            // AttributeMessage is optional
            // both may appear at the same time, or only of of them, or none of them
            // => do not use "if/else"

            // attributes are stored compactly
            var attributeMessages1 = this.ObjectHeader.GetMessages<AttributeMessage>();

            foreach (var attributeMessage in attributeMessages1)
            {
                yield return new H5Attribute(attributeMessage, this.File.Superblock);
            }

            // attributes are stored densely
            var attributeInfoMessages = this.ObjectHeader.GetMessages<AttributeInfoMessage>();

            if (attributeInfoMessages.Any())
            {
                if (attributeInfoMessages.Count() != 1)
                    throw new Exception("There may be only a single attribute info message.");

                var attributeInfoMessage = attributeInfoMessages.First();

                if (!this.File.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
                {
                    var attributeMessages2 = this.EnumerateAttributeMessagesFromAttributeInfoMessage(attributeInfoMessage);

                    foreach (var attributeMessage in attributeMessages2)
                    {
                        yield return new H5Attribute(attributeMessage, this.File.Superblock);
                    }
                }
            }
        }

        private IEnumerable<AttributeMessage> EnumerateAttributeMessagesFromAttributeInfoMessage(AttributeInfoMessage attributeInfoMessage)
        {
            var btree2NameIndex = attributeInfoMessage.BTree2NameIndex;
            var records = btree2NameIndex
                .EnumerateRecords()
                .ToList();

            var fractalHeap = attributeInfoMessage.FractalHeap;

            // local cache: indirectly accessed, non-filtered
            IEnumerable<BTree2Record01>? record01Cache = null;

            foreach (var record in records)
            {
#warning duplicate1
                using var localReader = new H5BinaryReader(new MemoryStream(record.HeapId));
                var heapId = FractalHeapId.Construct(this.File.Reader, this.File.Superblock, localReader, fractalHeap);
                var message = heapId.Read(reader => new AttributeMessage(reader, this.File.Superblock), ref record01Cache);

                yield return message;
            }
        }

        private bool TryGetAttributeMessageFromAttributeInfoMessage(AttributeInfoMessage attributeInfoMessage,
                                                                    string name,
                                                                    [NotNullWhen(returnValue: true)] out AttributeMessage? attributeMessage)
        {
            attributeMessage = null;

            var fractalHeap = attributeInfoMessage.FractalHeap;
            var btree2NameIndex = attributeInfoMessage.BTree2NameIndex;
            var nameHash = H5Checksum.JenkinsLookup3(name);
            var candidate = default(AttributeMessage);

            var success = btree2NameIndex.TryFindRecord(out var record, record =>
            {
#warning Better to implement comparison code in record (here: BTree2Record08) itself?

                if (nameHash < record.NameHash)
                {
                    return -1;
                }
                else if (nameHash > record.NameHash)
                {
                    return 1;
                }
                else
                {
#warning duplicate2
                    using var localReader = new H5BinaryReader(new MemoryStream(record.HeapId));
                    var heapId = FractalHeapId.Construct(this.File.Reader, this.File.Superblock, localReader, fractalHeap);
                    candidate = heapId.Read(reader => new AttributeMessage(reader, this.File.Superblock));

                    // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                    // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                    return string.CompareOrdinal(name, candidate.Name);
                }
            });

            if (success)
            {
                if (candidate == null)
                    throw new Exception("This should never happen. Just to satisfy the compiler.");

                attributeMessage = candidate;
                return true;
            }

            return false;
        }

        #endregion
    }
}

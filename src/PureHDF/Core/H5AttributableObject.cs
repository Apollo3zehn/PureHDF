﻿using System.Diagnostics.CodeAnalysis;

namespace PureHDF
{
    abstract partial class H5AttributableObject : H5Object
    {
        #region Constructors

        internal H5AttributableObject(H5Context context, NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            //
        }

        internal H5AttributableObject(H5Context context, NamedReference reference)
            : base(context, reference)
        {
            //
        }

        #endregion

        #region Methods

        private bool TryGetAttributeMessage(string name, [NotNullWhen(returnValue: true)] out AttributeMessage? attributeMessage)
        {
            // get attribute from attribute message
            attributeMessage = Header
                .GetMessages<AttributeMessage>()
                .FirstOrDefault(message => message.Name == name);

            if (attributeMessage is not null)
            {
                return true;
            }
            // get attribute from attribute info
            else
            {
                var attributeInfoMessages = Header.GetMessages<AttributeInfoMessage>();

                if (attributeInfoMessages.Any())
                {
                    if (attributeInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single attribute info message.");

                    var attributeInfoMessage = attributeInfoMessages.First();

                    if (!Context.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
                    {
                        if (TryGetAttributeMessageFromAttributeInfoMessage(attributeInfoMessage, name, out attributeMessage))
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
            var attributeMessages1 = Header.GetMessages<AttributeMessage>();

            foreach (var attributeMessage in attributeMessages1)
            {
                yield return new H5Attribute(Context, attributeMessage);
            }

            // attributes are stored densely
            var attributeInfoMessages = Header.GetMessages<AttributeInfoMessage>();

            if (attributeInfoMessages.Any())
            {
                if (attributeInfoMessages.Count() != 1)
                    throw new Exception("There may be only a single attribute info message.");

                var attributeInfoMessage = attributeInfoMessages.First();

                if (!Context.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
                {
                    var attributeMessages2 = EnumerateAttributeMessagesFromAttributeInfoMessage(attributeInfoMessage);

                    foreach (var attributeMessage in attributeMessages2)
                    {
                        yield return new H5Attribute(Context, attributeMessage);
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
            List<BTree2Record01>? record01Cache = null;

            foreach (var record in records)
            {
                // TODO: duplicate1_of_3
                using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
                var heapId = FractalHeapId.Construct(Context, localDriver, fractalHeap);
                var message = heapId.Read(driver => new AttributeMessage(Context, Header), ref record01Cache);

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
            var nameHash = ChecksumUtils.JenkinsLookup3(name);
            var candidate = default(AttributeMessage);

            var success = btree2NameIndex.TryFindRecord(out var record, record =>
            {
                // H5Abtree2.c (H5A__dense_btree2_name_compare, H5A__dense_fh_name_cmp)

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
                    // TODO: duplicate2_of_3
                    using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
                    var heapId = FractalHeapId.Construct(Context, localDriver, fractalHeap);
                    candidate = heapId.Read(driver => new AttributeMessage(Context, Header));

                    // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                    // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                    return string.CompareOrdinal(name, candidate.Name);
                }
            });

            if (success)
            {
                if (candidate is null)
                    throw new Exception("This should never happen. Just to satisfy the compiler.");

                attributeMessage = candidate;
                return true;
            }

            return false;
        }

        #endregion
    }
}

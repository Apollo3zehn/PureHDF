using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class ObjectHeader : FileBlock
    {
        #region Constructors

        public ObjectHeader(H5BinaryReader reader) : base(reader)
        {
            this.HeaderMessages = new List<HeaderMessage>();
        }

        #endregion

        #region Properties

        public List<HeaderMessage> HeaderMessages { get; }

        public H5ObjectType ObjectType { get; protected set; }

        #endregion

        #region Methods

        public T GetMessage<T>() where T : Message
        {
            return (T)this.HeaderMessages
                .First(message => message.Data.GetType() == typeof(T))
                .Data;
        }

        public IEnumerable<T> GetMessages<T>() where T : Message
        {
            return this.HeaderMessages
                .Where(message => message.Data.GetType() == typeof(T))
                .Select(message => message.Data)
                .Cast<T>();
        }

        internal static ObjectHeader Construct(H5Context context)
        {
            // get version
            var version = context.Reader.ReadByte();

            // must be a version 2+ object header
            if (version != 1)
            {
                var signature = new byte[] { version }.Concat(context.Reader.ReadBytes(3)).ToArray();
                H5Utils.ValidateSignature(signature, ObjectHeader2.Signature);
                version = context.Reader.ReadByte();
            }

            return version switch
            {
                1 => new ObjectHeader1(context, version),
                2 => new ObjectHeader2(context, version),
                _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
            };
        }

        private protected List<HeaderMessage> ReadHeaderMessages(H5Context context,
                                                                 ulong objectHeaderSize,
                                                                 byte version,
                                                                 bool withCreationOrder = false)
        {
            var headerMessages = new List<HeaderMessage>();
            var continuationMessages = new List<ObjectHeaderContinuationMessage>();
            var remainingBytes = objectHeaderSize;

            ulong prefixSize;
            ulong gapSize;

            if (version == 1)
            {
                prefixSize = 8UL;
                gapSize = 0;
            }    
            else if (version == 2)
            {
                prefixSize = 4UL + (withCreationOrder ? 2UL : 0UL);
                gapSize = prefixSize;
            }
            else
            {
                throw new Exception("The object header version number must be in the range of 1..2.");
            }

            while (remainingBytes > gapSize)
            {
                var message = new HeaderMessage(context, version, withCreationOrder);

                remainingBytes -= message.DataSize + prefixSize;

                if (message.Type == HeaderMessageType.ObjectHeaderContinuation)
                    continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);
                else
                    headerMessages.Add(message);
            }

            foreach (var continuationMessage in continuationMessages)
            {
                context.Reader.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);

                if (version == 1)
                {
                    var messages = this.ReadHeaderMessages(context, continuationMessage.Length, version);
                    headerMessages.AddRange(messages);
                }
                else if (version == 2)
                {
                    var continuationBlock = new ObjectHeaderContinuationBlock2(context, continuationMessage.Length, version, withCreationOrder);
                    var messages = continuationBlock.HeaderMessages;
                    headerMessages.AddRange(messages);
                }
            }

            this.ObjectType = this.DetermineObjectType(headerMessages);

            return headerMessages;
        }

        private H5ObjectType DetermineObjectType(List<HeaderMessage> headerMessages)
        {
            foreach (var message in headerMessages)
            {
                switch (message.Type)
                {
                    case HeaderMessageType.LinkInfo:
                    case HeaderMessageType.Link:
                    case HeaderMessageType.GroupInfo:
                    case HeaderMessageType.SymbolTable:
                        return H5ObjectType.Group;

                    case HeaderMessageType.DataLayout:
                        return H5ObjectType.Dataset;

                    default:
                        break;
                }
            }

            var condition = headerMessages.Count == 1 &&
                            headerMessages[0].Type == HeaderMessageType.Datatype;

            if (condition)
                return H5ObjectType.CommitedDatatype;
            else
                return H5ObjectType.Undefined;
        }

        #endregion
    }
}

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    public abstract class ObjectHeader : FileBlock
    {
        #region Constructors

        public ObjectHeader(BinaryReader reader) : base(reader)
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

        public List<T> GetMessages<T>() where T : Message
        {
            return this.HeaderMessages
                .Where(message => message.Data.GetType() == typeof(T))
                .Select(message => message.Data)
                .Cast<T>()
                .ToList();
        }

        public static ObjectHeader Construct(BinaryReader reader, Superblock superblock)
        {
            // get version
            var version = reader.ReadByte();

            // must be a version 2+ object header
            if (version != 1)
            {
                var signature = new byte[] { version }.Concat(reader.ReadBytes(3)).ToArray();
                H5Utils.ValidateSignature(signature, ObjectHeader2.Signature);
                version = reader.ReadByte();
            }

            return version switch
            {
                1 => new ObjectHeader1(reader, superblock, version),
                2 => new ObjectHeader2(reader, superblock, version),
                _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
            };
        }

        public override void Print(ILogger logger)
        {
            logger.LogInformation("ObjectHeader");

            for (int i = 0; i < this.HeaderMessages.Count; i++)
            {
                logger.LogInformation($"ObjectHeader HeaderMessage[{i}]");
                var message = this.HeaderMessages[i];
                message.Print(logger);

                if (message.Type == HeaderMessageType.SymbolTable)
                {
                    var symbolTableMessage = (SymbolTableMessage)message.Data;
                    symbolTableMessage.Print(logger);
                }
                else
                {
                    logger.LogInformation($"ObjectHeader HeaderMessage Type = {message.Type} is not supported yet. Stopping.");
                }
            }
        }

        protected List<HeaderMessage> ReadHeaderMessages(BinaryReader reader, Superblock superblock, ulong objectHeaderSize, byte version, bool withCreationOrder = false)
        {
            var headerMessages = new List<HeaderMessage>();
            var continuationMessages = new List<ObjectHeaderContinuationMessage>();
            var remainingBytes = objectHeaderSize;

            while (remainingBytes > 0)
            {
                var message = new HeaderMessage(reader, superblock, version, withCreationOrder);

                remainingBytes -= message.DataSize + version switch
                {
                    1 => 8UL,
                    2 => 4UL + (withCreationOrder ? 2UL : 0UL),
                    _ => throw new Exception("The object header version number must be in the range of 1..2.")
                };

                if (message.Type == HeaderMessageType.ObjectHeaderContinuation)
                {
                    continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);
                }
                else
                {
                    headerMessages.Add(message);

                    switch (message.Type)
                    {
                        case HeaderMessageType.LinkInfo:
                        case HeaderMessageType.Link:
                        case HeaderMessageType.GroupInfo:
                        case HeaderMessageType.SymbolTable:
                            this.ObjectType = H5ObjectType.Group;
                            break;

                        case HeaderMessageType.DataLayout:
                            this.ObjectType = H5ObjectType.Dataset;
                            break;

                        default:
                            break;
                    }
                }
            }

            foreach (var continuationMessage in continuationMessages)
            {
                this.Reader.BaseStream.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);
                var messages = this.ReadHeaderMessages(reader, superblock, continuationMessage.Length, version);
                headerMessages.AddRange(messages);
            }

            var condition = this.ObjectType == H5ObjectType.Undefined
                            && headerMessages.Count == 1
                            && headerMessages[0].Type == HeaderMessageType.DataType;

            if (condition)
                this.ObjectType = H5ObjectType.CommitedDataType;

            return headerMessages;
        }

        #endregion
    }
}

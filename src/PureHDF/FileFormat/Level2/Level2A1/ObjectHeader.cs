namespace PureHDF
{
    internal abstract class ObjectHeader
    {
        #region Fields

        H5Context _context;

        #endregion

        #region Constructors

        public ObjectHeader(H5Context context)
        {
            _context = context;

            Address = (ulong)context.Reader.Position;
            HeaderMessages = new List<HeaderMessage>();
        }

        #endregion

        #region Properties

        public List<HeaderMessage> HeaderMessages { get; }

        public ObjectType ObjectType { get; protected set; }

        public ulong Address { get; }

        #endregion

        #region Methods

        // TODO: This method could als be static or moved to another type. Is does not strictly belong to object header. Only "Address" is required from object header.
        public T DecodeMessage<T>(MessageFlags messageFlags, Func<T> decode) where T : Message
        {
            // H5OShared.h (H5O_SHARED_DECODE)

            if (messageFlags.HasFlag(MessageFlags.Shared))
            {
                var sharedMessage = new SharedMessage(_context);
                return DecodeSharedMessage<T>(sharedMessage);
            }
            else
            {
                return decode();
            }
        }

        public T GetMessage<T>() where T : Message
        {
            return (T)HeaderMessages
                .First(message => message.Data.GetType() == typeof(T))
                .Data;
        }

        public IEnumerable<T> GetMessages<T>() where T : Message
        {
            return HeaderMessages
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
                var message = new HeaderMessage(context, version, this, withCreationOrder);

                remainingBytes -= message.DataSize + prefixSize;

                if (message.Type == MessageType.ObjectHeaderContinuation)
                    continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);
                else
                    headerMessages.Add(message);
            }

            foreach (var continuationMessage in continuationMessages)
            {
                context.Reader.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);

                if (version == 1)
                {
                    var messages = ReadHeaderMessages(context, continuationMessage.Length, version);
                    headerMessages.AddRange(messages);
                }
                else if (version == 2)
                {
                    var continuationBlock = new ObjectHeaderContinuationBlock2(context, continuationMessage.Length, version, withCreationOrder);
                    var messages = continuationBlock.HeaderMessages;
                    headerMessages.AddRange(messages);
                }
            }

            ObjectType = DetermineObjectType(headerMessages);

            return headerMessages;
        }

        private static ObjectType DetermineObjectType(List<HeaderMessage> headerMessages)
        {
            foreach (var message in headerMessages)
            {
                switch (message.Type)
                {
                    case MessageType.LinkInfo:
                    case MessageType.Link:
                    case MessageType.GroupInfo:
                    case MessageType.SymbolTable:
                        return ObjectType.Group;

                    case MessageType.DataLayout:
                        return ObjectType.Dataset;

                    default:
                        break;
                }
            }

            foreach (var message in headerMessages)
            {
                switch (message.Type)
                {
                    case MessageType.Datatype:
                        return ObjectType.CommitedDatatype;
                }
            }

            return ObjectType.Undefined;
        }

        private T DecodeSharedMessage<T>(SharedMessage message) where T : Message
        {
            // H5Oshared.c (H5O__shared_read)

            /* Check for implicit shared object header message*/
            if (message.Type == SharedMessageLocation.SharedObjectHeaderMessageHeap)
            {
                // TODO: Implement 
                throw new NotImplementedException("This code path is not yet implemented.");
            }
            else
            {
                if (message.Address == Address)
                {
                    /* The shared message is in the already opened object header.  This
                     * is possible, for example, if an attribute's datatype is shared in
                     * the same object header the attribute is in.  Read the message
                     * directly. */
                    // TODO: Implement 
                    throw new NotImplementedException("This code path is not yet implemented.");
                }
                else
                {
                    /* The shared message is in another object header */

                    // TODO: This would greatly benefit from a caching mechanism!
                    var address = _context.Reader.Position;
                    _context.Reader.Seek((long)message.Address, SeekOrigin.Begin);

                    var header = ObjectHeader.Construct(_context);
                    var sharedMessage = header.GetMessage<T>();

                    _context.Reader.Seek(address, SeekOrigin.Begin);

                    return sharedMessage;
                }
            }
        }


        #endregion
    }
}

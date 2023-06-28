namespace PureHDF.VOL.Native;

internal abstract record class ObjectHeader(
    ulong Address,
    List<HeaderMessage> HeaderMessages
)
{
    private ObjectType _objectType;

    public ObjectType ObjectType
    {
        get {
            if (_objectType == ObjectType.Undefined)
                _objectType = DetermineObjectType(HeaderMessages);

            return _objectType;
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

    internal static ObjectHeader Construct(NativeContext context)
    {
        // get version
        var version = context.Driver.ReadByte();

        // must be a version 2+ object header
        if (version != 1)
        {
            var signature = new byte[] { version }.Concat(context.Driver.ReadBytes(3)).ToArray();
            Utils.ValidateSignature(signature, ObjectHeader2.Signature);
            version = context.Driver.ReadByte();
        }

        return version switch
        {
            1 => ObjectHeader1.Decode(context, version),
            2 => ObjectHeader2.Decode(context, version),
            _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
        };
    }

    private protected static List<HeaderMessage> ReadHeaderMessages(
        NativeContext context,
        ulong objectHeaderAddress,
        ulong objectHeaderSize,
        byte version,
        bool withCreationOrder)
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
            var message = HeaderMessage
                .Decode(context, version, objectHeaderAddress, withCreationOrder);

            remainingBytes -= message.DataSize + prefixSize;

            if (message.Type == MessageType.ObjectHeaderContinuation)
                continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);

            else
                headerMessages.Add(message);
        }

        foreach (var continuationMessage in continuationMessages)
        {
            context.Driver.Seek((long)continuationMessage.Offset, SeekOrigin.Begin);

            if (version == 1)
            {
                var moreHeaderMessages = ReadHeaderMessages(
                    context,
                    objectHeaderAddress,
                    continuationMessage.Length, 
                    version, 
                    withCreationOrder: false);

                headerMessages.AddRange(moreHeaderMessages);
            }
            else if (version == 2)
            {
                var continuationBlock = ObjectHeaderContinuationBlock2.Decode(
                    context, 
                    objectHeaderAddress,
                    continuationMessage.Length, 
                    version, 
                    withCreationOrder);

                headerMessages.AddRange(continuationBlock.HeaderMessages);
            }
        }

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
}
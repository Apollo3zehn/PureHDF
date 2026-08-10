namespace PureHDF.VOL.Native;

internal abstract record class ObjectHeader(
    ulong Address,
    List<HeaderMessage> HeaderMessages
)
{
    private ObjectType _objectType;

    public ObjectType ObjectType
    {
        get
        {
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

    internal static async ValueTask<ObjectHeader> Construct(NativeReadContext context)
    {
        // get version
        var version = await context.Driver.ReadByte().ConfigureAwait(false);

        // must be a version 2+ object header
        if (version != 1)
        {
            var signature = new byte[] { version }.Concat(await context.Driver.ReadBytes(3).ConfigureAwait(false)).ToArray();
            MathUtils.ValidateSignature(signature, ObjectHeader2.Signature);
            version = await context.Driver.ReadByte().ConfigureAwait(false);
        }

        return version switch
        {
            1 => await ObjectHeader1.Decode(context, version).ConfigureAwait(false),
            2 => await ObjectHeader2.Decode(context, version).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
        };
    }

    private protected static async ValueTask<List<HeaderMessage>> ReadHeaderMessages(
        NativeReadContext context,
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
            var message = await HeaderMessage
                .Decode(context, version, objectHeaderAddress, withCreationOrder)
                .ConfigureAwait(false);

            remainingBytes -= message.DataSize + prefixSize;

            if (message.Type == MessageType.ObjectHeaderContinuation)
                continuationMessages.Add((ObjectHeaderContinuationMessage)message.Data);

            else
                headerMessages.Add(message);
        }

        foreach (var continuationMessage in continuationMessages)
        {
            context.Driver.SeekRelativeToBaseAddress((long)continuationMessage.Offset);

            if (version == 1)
            {
                var moreHeaderMessages = await ReadHeaderMessages(
                    context,
                    objectHeaderAddress,
                    continuationMessage.Length,
                    version,
                    withCreationOrder: false).ConfigureAwait(false);

                headerMessages.AddRange(moreHeaderMessages);
            }
            else if (version == 2)
            {
                var continuationBlock = await ObjectHeaderContinuationBlock2.Decode(
                    context,
                    objectHeaderAddress,
                    continuationMessage.Length,
                    version,
                    withCreationOrder).ConfigureAwait(false);

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
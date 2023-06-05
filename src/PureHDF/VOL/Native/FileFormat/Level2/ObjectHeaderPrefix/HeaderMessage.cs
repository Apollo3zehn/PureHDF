namespace PureHDF.VOL.Native;

internal readonly record struct HeaderMessage(
    MessageType Type,
    ushort DataSize,
    MessageFlags Flags,
    ushort CreationOrder,
    Message Data
)
{
    private readonly byte _version;

    private readonly bool _withCreationOrder;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 2))
                throw new NotSupportedException("The header message version number must be in the range of 1..2.");

            _version = value;
        }
    }

    public required bool WithCreationOrder
    {
        get
        {
            return _withCreationOrder;
        }
        init
        {
            if (Version == 1 && value)
                throw new FormatException("Only version 2 header messages are allowed to have 'WithCreationOrder' set to true.");

            _withCreationOrder = value;
        }
    }

    internal static HeaderMessage Decode(
        NativeContext context, 
        byte version,
        ObjectHeader objectHeader, 
        bool withCreationOrder = false)
    {
        // message type
        var type = MessageType.NIL;

        if (version == 1)
            type = (MessageType)context.Driver.ReadUInt16();

        else if (version == 2)
            type = (MessageType)context.Driver.ReadByte();

        // data size
        var dataSize = context.Driver.ReadUInt16();

        // flags
        var flags = (MessageFlags)context.Driver.ReadByte();

        // reserved / creation order
        var creationOrder = default(ushort);

        if (version == 1)
            context.Driver.ReadBytes(3);

        else if (version == 2 && withCreationOrder)
            creationOrder = context.Driver.ReadUInt16();

        // data
        var driverPosition1 = context.Driver.Position;

        /* Search for "H5O_SHARED_DECODE_REAL" in C-code to find all shareable messages */

        Message data = type switch
        {
            MessageType.NIL => new NilMessage(),
            MessageType.Dataspace => Message.Decode(context, objectHeader.Address, flags, () => new DataspaceMessage(context)),
            MessageType.LinkInfo => new LinkInfoMessage(context),
            MessageType.Datatype => Message.Decode(context, objectHeader.Address, flags, () => new DatatypeMessage(context.Driver)),
            MessageType.OldFillValue => Message.Decode(context, objectHeader.Address, flags, () => new OldFillValueMessage(context.Driver)),
            MessageType.FillValue => Message.Decode(context, objectHeader.Address, flags, () => new FillValueMessage(context.Driver)),
            MessageType.Link => new LinkMessage(context),
            MessageType.ExternalDataFiles => new ExternalFileListMessage(context),
            MessageType.DataLayout => DataLayoutMessage.Construct(context),
            MessageType.Bogus => new BogusMessage(context.Driver),
            MessageType.GroupInfo => new GroupInfoMessage(context.Driver),
            MessageType.FilterPipeline => Message.Decode(context, objectHeader.Address, flags, () => new FilterPipelineMessage(context.Driver)),
            MessageType.Attribute => Message.Decode(context, objectHeader.Address, flags, () => new AttributeMessage(context, objectHeader)),
            MessageType.ObjectComment => new ObjectCommentMessage(context.Driver),
            MessageType.OldObjectModificationTime => new OldObjectModificationTimeMessage(context.Driver).ToObjectModificationMessage(),
            MessageType.SharedMessageTable => new SharedMessageTableMessage(context),
            MessageType.ObjectHeaderContinuation => new ObjectHeaderContinuationMessage(context),
            MessageType.SymbolTable => new SymbolTableMessage(context),
            MessageType.ObjectModification => new ObjectModificationMessage(context.Driver),
            MessageType.BTreeKValues => new BTreeKValuesMessage(context.Driver),
            MessageType.DriverInfo => new DriverInfoMessage(context.Driver),
            MessageType.AttributeInfo => new AttributeInfoMessage(context),
            MessageType.ObjectReferenceCount => new ObjectReferenceCountMessage(context.Driver),
            _ => throw new NotSupportedException($"The message type '{type}' is not supported.")
        };

        var driverPosition2 = context.Driver.Position;
        var paddingBytes = dataSize - (driverPosition2 - driverPosition1);

        context.Driver.ReadBytes((int)paddingBytes);

        return new HeaderMessage(
            type,
            dataSize,
            flags,
            creationOrder,
            data
        )
        {
            Version = version,
            WithCreationOrder = withCreationOrder
        };
    }
}
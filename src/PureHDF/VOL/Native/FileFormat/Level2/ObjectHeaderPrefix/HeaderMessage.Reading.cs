namespace PureHDF.VOL.Native;

internal readonly partial record struct HeaderMessage(
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
        NativeReadContext context, 
        byte version,
        ulong objectHeaderAddress, 
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
            MessageType.Dataspace => Message.Decode(context, objectHeaderAddress, flags, () => DataspaceMessage.Decode(context)),
            MessageType.LinkInfo => LinkInfoMessage.Decode(context),
            MessageType.Datatype => Message.Decode(context, objectHeaderAddress, flags, () => DatatypeMessage.Decode(context.Driver)),
            MessageType.OldFillValue => Message.Decode(context, objectHeaderAddress, flags, () => OldFillValueMessage.Decode(context.Driver)),
            MessageType.FillValue => Message.Decode(context, objectHeaderAddress, flags, () => FillValueMessage.Decode(context.Driver)),
            MessageType.Link => LinkMessage.Decode(context),
            MessageType.ExternalDataFiles => ExternalFileListMessage.Decode(context),
            MessageType.DataLayout => DataLayoutMessage.Construct(context),
            MessageType.Bogus => BogusMessage.Decode(context.Driver),
            MessageType.GroupInfo => GroupInfoMessage.Decode(context.Driver),
            MessageType.FilterPipeline => Message.Decode(context, objectHeaderAddress, flags, () => FilterPipelineMessage.Decode(context.Driver)),
            MessageType.Attribute => Message.Decode(context, objectHeaderAddress, flags, () => AttributeMessage.Decode(context, objectHeaderAddress)),
            MessageType.ObjectComment => ObjectCommentMessage.Decode(context.Driver),
            MessageType.OldObjectModificationTime => OldObjectModificationTimeMessage.Decode(context.Driver).ToObjectModificationMessage(),
            MessageType.SharedMessageTable => SharedMessageTableMessage.Decode(context),
            MessageType.ObjectHeaderContinuation => ObjectHeaderContinuationMessage.Decode(context),
            MessageType.SymbolTable => SymbolTableMessage.Decode(context),
            MessageType.ObjectModification => ObjectModificationMessage.Decode(context.Driver),
            MessageType.BTreeKValues => BTreeKValuesMessage.Decode(context.Driver),
            MessageType.DriverInfo => DriverInfoMessage.Decode(context.Driver),
            MessageType.AttributeInfo => AttributeInfoMessage.Decode(context),
            MessageType.ObjectReferenceCount => ObjectReferenceCountMessage.Decode(context.Driver),
            _ => throw new NotSupportedException($"The message type '{type}' is not supported.")
        };

        var driverPosition2 = context.Driver.Position;
        var paddingBytes = dataSize - (driverPosition2 - driverPosition1);

        if (paddingBytes < 0)
            throw new Exception("Unexpected HDF5 file data.");

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
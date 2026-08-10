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

    internal static async ValueTask<HeaderMessage> Decode(
        NativeReadContext context,
        byte version,
        ulong objectHeaderAddress,
        bool withCreationOrder = false)
    {
        // message type
        var type = MessageType.NIL;

        if (version == 1)
            type = (MessageType)await context.Driver.ReadUInt16().ConfigureAwait(false);

        else if (version == 2)
            type = (MessageType)await context.Driver.ReadByte().ConfigureAwait(false);

        // data size
        var dataSize = await context.Driver.ReadUInt16().ConfigureAwait(false);

        // flags
        var flags = (MessageFlags)await context.Driver.ReadByte().ConfigureAwait(false);

        // reserved / creation order
        var creationOrder = default(ushort);

        if (version == 1)
            await context.Driver.ReadBytes(3).ConfigureAwait(false);

        else if (version == 2 && withCreationOrder)
            creationOrder = await context.Driver.ReadUInt16().ConfigureAwait(false);

        // data
        var driverPosition1 = context.Driver.Position;

        /* Search for "H5O_SHARED_DECODE_REAL" in C-code to find all shareable messages */

        Message data = type switch
        {
            MessageType.NIL => new NilMessage(),
            MessageType.Dataspace => await Message.Decode(context, objectHeaderAddress, flags, () => DataspaceMessage.Decode(context)).ConfigureAwait(false),
            MessageType.LinkInfo => await LinkInfoMessage.Decode(context).ConfigureAwait(false),
            MessageType.Datatype => await Message.Decode(context, objectHeaderAddress, flags, () => DatatypeMessage.Decode(context.Driver)).ConfigureAwait(false),
            MessageType.OldFillValue => await Message.Decode(context, objectHeaderAddress, flags, () => OldFillValueMessage.Decode(context.Driver)).ConfigureAwait(false),
            MessageType.FillValue => await Message.Decode(context, objectHeaderAddress, flags, () => FillValueMessage.Decode(context.Driver)).ConfigureAwait(false),
            MessageType.Link => await LinkMessage.Decode(context).ConfigureAwait(false),
            MessageType.ExternalDataFiles => await ExternalFileListMessage.Decode(context).ConfigureAwait(false),
            MessageType.DataLayout => await DataLayoutMessage.Construct(context).ConfigureAwait(false),
            MessageType.Bogus => await BogusMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.GroupInfo => await GroupInfoMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.FilterPipeline => await Message.Decode(context, objectHeaderAddress, flags, () => FilterPipelineMessage.Decode(context.Driver)).ConfigureAwait(false),
            MessageType.Attribute => await Message.Decode(context, objectHeaderAddress, flags, () => AttributeMessage.Decode(context, objectHeaderAddress)).ConfigureAwait(false),
            MessageType.ObjectComment => await ObjectCommentMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.OldObjectModificationTime => (await OldObjectModificationTimeMessage.Decode(context.Driver).ConfigureAwait(false)).ToObjectModificationMessage(),
            MessageType.SharedMessageTable => await SharedMessageTableMessage.Decode(context).ConfigureAwait(false),
            MessageType.ObjectHeaderContinuation => await ObjectHeaderContinuationMessage.Decode(context).ConfigureAwait(false),
            MessageType.SymbolTable => await SymbolTableMessage.Decode(context).ConfigureAwait(false),
            MessageType.ObjectModification => await ObjectModificationMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.BTreeKValues => await BTreeKValuesMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.DriverInfo => await DriverInfoMessage.Decode(context.Driver).ConfigureAwait(false),
            MessageType.AttributeInfo => await AttributeInfoMessage.Decode(context).ConfigureAwait(false),
            MessageType.ObjectReferenceCount => await ObjectReferenceCountMessage.Decode(context.Driver).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The message type '{type}' is not supported.")
        };

        var driverPosition2 = context.Driver.Position;
        var paddingBytes = dataSize - (driverPosition2 - driverPosition1);

        if (paddingBytes < 0)
            throw new Exception("Unexpected HDF5 file data.");

        await context.Driver.ReadBytes((int)paddingBytes).ConfigureAwait(false);

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
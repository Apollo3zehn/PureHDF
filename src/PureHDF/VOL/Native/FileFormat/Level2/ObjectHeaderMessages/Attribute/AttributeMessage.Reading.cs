namespace PureHDF.VOL.Native;

internal partial record class AttributeMessage(
    AttributeMessageFlags Flags,
    string Name,
    DatatypeMessage Datatype,
    DataspaceMessage Dataspace,
    Memory<byte> InputData,
    Action<H5DriverBase> EncodeData
) : Message
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1 - 3 instances of type {nameof(AttributeMessage)} are supported.");

            _version = value;
        }
    }

    public static async ValueTask<AttributeMessage> Decode(NativeReadContext context, ulong objectHeaderAddress)
    {
        // version
        var version = await context.Driver.ReadByte().ConfigureAwait(false);

        // flags
        var flags = default(AttributeMessageFlags);

        if (version == 1)
            await context.Driver.ReadByte().ConfigureAwait(false);

        else
            flags = (AttributeMessageFlags)await context.Driver.ReadByte().ConfigureAwait(false);

        // name size
        var nameSize = await context.Driver.ReadUInt16().ConfigureAwait(false);

        // datatype size
        var datatypeSize = await context.Driver.ReadUInt16().ConfigureAwait(false);

        // dataspace size
        var dataspaceSize = await context.Driver.ReadUInt16().ConfigureAwait(false);

        // name character set encoding
        // The field is consumed to keep the driver aligned but not acted upon: names are
        // decoded as UTF-8 either way, which is correct for ASCII too. See ReadUtils.
        if (version == 3)
            _ = await context.Driver.ReadByte().ConfigureAwait(false);

        // name
        string name;

        if (version == 1)
            name = await ReadUtils.ReadNullTerminatedString(context.Driver, pad: true).ConfigureAwait(false);

        else
            name = await ReadUtils.ReadNullTerminatedString(context.Driver, pad: false).ConfigureAwait(false);

        // datatype
        var flags1 = flags.HasFlag(AttributeMessageFlags.SharedDatatype)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        var datatype = await Decode(context, objectHeaderAddress, flags1,
            () => DatatypeMessage.Decode(context.Driver)).ConfigureAwait(false);

        if (version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(datatypeSize / 8.0) * 8);
            var remainingSize = paddedSize - datatypeSize;
            await context.Driver.ReadBytes(remainingSize).ConfigureAwait(false);
        }

        // dataspace
        var flags2 = flags.HasFlag(AttributeMessageFlags.SharedDataspace)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        var dataspace = await Decode(context, objectHeaderAddress, flags2,
            () => DataspaceMessage.Decode(context)).ConfigureAwait(false);

        if (version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
            var remainingSize = paddedSize - dataspaceSize;
            context.Driver.Seek(remainingSize, SeekOrigin.Current);
        }

        // data
        var byteSize = dataspace.GetTotalElementCount() * datatype.Size;
        var data = await context.Driver.ReadBytes((int)byteSize).ConfigureAwait(false);

        return new AttributeMessage(
            Flags: flags,
            Name: name,
            Datatype: datatype,
            Dataspace: dataspace,
            InputData: data,
            EncodeData: default!
        )
        {
            Version = version
        };
    }

    private DatatypeMessage? ReadSharedMessage(ObjectHeader objectHeader, SharedMessage sharedMessage)
    {
        throw new NotImplementedException();
    }
}
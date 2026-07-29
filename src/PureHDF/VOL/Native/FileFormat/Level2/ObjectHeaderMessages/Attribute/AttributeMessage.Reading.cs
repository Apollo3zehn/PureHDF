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

    public static AttributeMessage Decode(NativeReadContext context, ulong objectHeaderAddress)
    {
        // version
        var version = context.Driver.ReadByte();

        // flags
        var flags = default(AttributeMessageFlags);

        if (version == 1)
            context.Driver.ReadByte();

        else
            flags = (AttributeMessageFlags)context.Driver.ReadByte();

        // name size
        var nameSize = context.Driver.ReadUInt16();

        // datatype size
        var datatypeSize = context.Driver.ReadUInt16();

        // dataspace size
        var dataspaceSize = context.Driver.ReadUInt16();

        // name character set encoding
        // The field is consumed to keep the driver aligned but not acted upon: names are
        // decoded as UTF-8 either way, which is correct for ASCII too. See ReadUtils.
        if (version == 3)
            _ = context.Driver.ReadByte();

        // name
        string name;

        if (version == 1)
            name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: true);

        else
            name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: false);

        // datatype
        var flags1 = flags.HasFlag(AttributeMessageFlags.SharedDatatype)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        var datatype = Decode(context, objectHeaderAddress, flags1,
            () => DatatypeMessage.Decode(context.Driver));

        if (version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(datatypeSize / 8.0) * 8);
            var remainingSize = paddedSize - datatypeSize;
            context.Driver.ReadBytes(remainingSize);
        }

        // dataspace 
        var flags2 = flags.HasFlag(AttributeMessageFlags.SharedDataspace)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        var dataspace = Decode(context, objectHeaderAddress, flags2,
            () => DataspaceMessage.Decode(context));

        if (version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
            var remainingSize = paddedSize - dataspaceSize;
            context.Driver.Seek(remainingSize, SeekOrigin.Current);
        }

        // data
        var byteSize = dataspace.GetTotalElementCount() * datatype.Size;
        var data = context.Driver.ReadBytes((int)byteSize);

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
namespace PureHDF.VOL.Native;

internal record class AttributeMessage(
    AttributeMessageFlags Flags,
    string Name,
    DatatypeMessage Datatype,
    DataspaceMessage Dataspace,
    byte[] Data
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

    public static AttributeMessage Decode(NativeContext context, ObjectHeader objectHeader)
    {
        // version
        var version = context.Driver.ReadByte();
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
        var nameEncoding = default(CharacterSetEncoding);

        if (version == 3)
            nameEncoding = (CharacterSetEncoding)context.Driver.ReadByte();

        // name
        #if ANONYMIZE
            var position1 = (long)context.Driver.Position;
        #endif

        string name;

        if (version == 1)
            name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: true, encoding: nameEncoding);

        else
            name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: false, encoding: nameEncoding);

        #if ANONYMIZE
            AnonymizeHelper.Append(
                "attribute-name", 
                context.Superblock.FilePath, 
                position1, 
                System.Text.Encoding.UTF8.GetBytes(name).Length,
                addBaseAddress: false);
        #endif

        // datatype
        var flags1 = flags.HasFlag(AttributeMessageFlags.SharedDatatype)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        var datatype = Decode(context, objectHeader.Address, flags1,
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

        var dataspace = Decode(context, objectHeader.Address, flags2,
            () => DataspaceMessage.Decode(context));

        if (version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
            var remainingSize = paddedSize - dataspaceSize;
            context.Driver.Seek(remainingSize, SeekOrigin.Current);
        }

        // data
        var byteSize = Utils.CalculateSize(dataspace.DimensionSizes, dataspace.Type) * datatype.Size;

        #if ANONYMIZE
            AnonymizeHelper.Append(
                "attribute-data", 
                context.Superblock.FilePath, 
                (long)context.Driver.Position, 
                (long)byteSize,
                addBaseAddress: false);
        #endif

        var data = context.Driver.ReadBytes((int)byteSize);

        return new AttributeMessage(
            Flags: flags,
            Name: name,
            Datatype: datatype,
            Dataspace: dataspace,
            Data: data
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
namespace PureHDF.VOL.Native;

internal class AttributeMessage : Message
{
    #region Fields

    private byte _version;
    private readonly CharacterSetEncoding _nameEncoding;
    private H5Context _context;

    #endregion

    #region Constructors

    public AttributeMessage(H5Context context, ObjectHeader objectHeader)
    {
        _context = context;

        // version
        Version = context.Driver.ReadByte();

        if (Version == 1)
            context.Driver.ReadByte();
        else
            Flags = (AttributeMessageFlags)context.Driver.ReadByte();

        // name size
        var nameSize = context.Driver.ReadUInt16();

        // datatype size
        var datatypeSize = context.Driver.ReadUInt16();

        // dataspace size
        var dataspaceSize = context.Driver.ReadUInt16();

        // name character set encoding
        if (Version == 3)
            _nameEncoding = (CharacterSetEncoding)context.Driver.ReadByte();

        // name
        if (Version == 1)
            Name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: true, encoding: _nameEncoding);
        else
            Name = ReadUtils.ReadNullTerminatedString(context.Driver, pad: false, encoding: _nameEncoding);

        // datatype
        var flags1 = Flags.HasFlag(AttributeMessageFlags.SharedDatatype)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        Datatype = objectHeader.DecodeMessage(flags1,
            () => new DatatypeMessage(context.Driver));

        if (Version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(datatypeSize / 8.0) * 8);
            var remainingSize = paddedSize - datatypeSize;
            context.Driver.ReadBytes(remainingSize);
        }

        // dataspace 
        var flags2 = Flags.HasFlag(AttributeMessageFlags.SharedDataspace)
            ? MessageFlags.Shared
            : MessageFlags.NoFlags;

        Dataspace = objectHeader.DecodeMessage(flags2,
            () => new DataspaceMessage(context));

        if (Version == 1)
        {
            var paddedSize = (int)(Math.Ceiling(dataspaceSize / 8.0) * 8);
            var remainingSize = paddedSize - dataspaceSize;
            _context.Driver.Seek(remainingSize, SeekOrigin.Current);
        }

        // data
        var byteSize = Utils.CalculateSize(Dataspace.DimensionSizes, Dataspace.Type) * Datatype.Size;
        Data = context.Driver.ReadBytes((int)byteSize);
    }

    private DatatypeMessage? ReadSharedMessage(ObjectHeader objectHeader, SharedMessage sharedMessage)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1 - 3 instances of type {nameof(AttributeMessage)} are supported.");

            _version = value;
        }
    }

    public AttributeMessageFlags Flags { get; set; }

    public string Name { get; set; }

    public DatatypeMessage Datatype { get; set; }

    public DataspaceMessage Dataspace { get; set; }

    public byte[] Data { get; set; }

    #endregion
}
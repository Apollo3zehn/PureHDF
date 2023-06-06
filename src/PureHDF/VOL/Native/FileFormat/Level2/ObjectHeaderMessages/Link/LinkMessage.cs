namespace PureHDF.VOL.Native;

internal record class LinkMessage(
    byte Flags,
    LinkType LinkType,
    ulong CreationOrder,
    string LinkName,
    LinkInfo LinkInfo
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
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(LinkMessage)} are supported.");

            _version = value;
        }
    }

    public static LinkMessage Decode(NativeContext context)
    {
        var (driver, _) = context;

        // version
        var version = driver.ReadByte();

        // flags
        var flags = driver.ReadByte();

        // link type
        var isLinkTypeFieldPresent = (flags & (1 << 3)) > 0;
        var linkType = default(LinkType);

        if (isLinkTypeFieldPresent)
            linkType = (LinkType)driver.ReadByte();

        // creation order
        var isCreationOrderFieldPresent = (flags & (1 << 2)) > 0;
        var creationOrder = default(ulong);

        if (isCreationOrderFieldPresent)
            creationOrder = driver.ReadUInt64();

        // link name encoding
        var isLinkNameEncodingFieldPresent = (flags & (1 << 4)) > 0;
        var linkNameEncoding = default(CharacterSetEncoding);

        if (isLinkNameEncodingFieldPresent)
            linkNameEncoding = (CharacterSetEncoding)driver.ReadByte();

        // link length
        var linkLengthFieldLength = (ulong)(1 << (flags & 0x03));
        var linkNameLength = Utils.ReadUlong(driver, linkLengthFieldLength);

        // link name
        var linkName = ReadUtils.ReadFixedLengthString(driver, (int)linkNameLength, linkNameEncoding);

        // link info
        LinkInfo linkInfo = linkType switch
        {
            LinkType.Hard => HardLinkInfo.Decode(context),
            LinkType.Soft => SoftLinkInfo.Decode(driver),
            LinkType.External => ExternalLinkInfo.Decode(driver),
            _ when 65 <= (byte)linkType && (byte)linkType <= 255 => UserDefinedLinkInfo.Decode(driver),
            _ => throw new NotSupportedException($"The link message link type '{linkType}' is not supported.")
        };

        return new LinkMessage(
            Flags: flags,
            LinkType: linkType,
            CreationOrder: creationOrder,
            LinkName: linkName,
            LinkInfo: linkInfo
        )
        {
            Version = version
        };
    }
}
namespace PureHDF.VOL.Native;

internal partial record class LinkMessage(
    LinkInfoFlags Flags,
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

    public static async ValueTask<LinkMessage> Decode(NativeReadContext context)
    {
        var (driver, _) = context;

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // flags
        var flags = (LinkInfoFlags)await driver.ReadByte().ConfigureAwait(false);

        // link type
        var linkType = default(LinkType);

        if (flags.HasFlag(LinkInfoFlags.LinkTypeFieldIsPresent))
            linkType = (LinkType)await driver.ReadByte().ConfigureAwait(false);

        // creation order
        var creationOrder = default(ulong);

        if (flags.HasFlag(LinkInfoFlags.CreationOrderFieldIsPresent))
            creationOrder = await driver.ReadUInt64().ConfigureAwait(false);

        // link name encoding
        // The field is consumed to keep the driver aligned but not acted upon: names are
        // decoded as UTF-8 either way, which is correct for ASCII too. See ReadUtils.
        if (flags.HasFlag(LinkInfoFlags.LinkNameEncodingFieldIsPresent))
            _ = await driver.ReadByte().ConfigureAwait(false);

        // link length
        var linkLengthFieldLength = (ulong)(1 << ((byte)flags & 0x03));
        var linkNameLength = await ReadUtils.ReadUlong(driver, linkLengthFieldLength).ConfigureAwait(false);

        // link name
        var linkName = await ReadUtils.ReadFixedLengthString(driver, (int)linkNameLength).ConfigureAwait(false);

        // link info
        LinkInfo linkInfo = linkType switch
        {
            LinkType.Hard => await HardLinkInfo.Decode(context).ConfigureAwait(false),
            LinkType.Soft => await SoftLinkInfo.Decode(driver).ConfigureAwait(false),
            LinkType.External => await ExternalLinkInfo.Decode(driver).ConfigureAwait(false),
            _ when 65 <= (byte)linkType && (byte)linkType <= 255 => await UserDefinedLinkInfo.Decode(driver).ConfigureAwait(false),
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
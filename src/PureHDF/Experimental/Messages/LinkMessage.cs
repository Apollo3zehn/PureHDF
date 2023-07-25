using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class LinkMessage
{
    public override void Encode(BinaryWriter driver)
    {
        // version
        driver.Write(Version);

        // flags
        driver.Write((byte)Flags);

        // link type
        if (Flags.HasFlag(LinkInfoFlags.LinkTypeFieldIsPresent))
            driver.Write((byte)LinkType);

        // creation order
        if (Flags.HasFlag(LinkInfoFlags.CreationOrderFieldIsPresent))
            driver.Write(CreationOrder);

        // link name encoding
        if (Flags.HasFlag(LinkInfoFlags.LinkNameEncodingFieldIsPresent))
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // link length
        var encodedName = Encoding.UTF8.GetBytes(LinkName);
        var linkNameFieldLength = GetLinkNameFieldLength();
        WriteUtils.WriteUlongArbitrary(driver, (ulong)encodedName.Length, linkNameFieldLength);

        // link name
        if (encodedName.Length > ushort.MaxValue)
            throw new Exception("The link name is too long.");

        driver.Write(encodedName);

        // link info
        LinkInfo.Encode(driver);
    }

    public override ushort GetEncodeSize()
    {
        var encodedNameLength = Encoding.UTF8.GetBytes(LinkName).Length;
        var linkNameFieldLength = GetLinkNameFieldLength();

        var size =
            sizeof(byte) +
            sizeof(byte) +
            (
                Flags.HasFlag(LinkInfoFlags.LinkTypeFieldIsPresent)
                    ? sizeof(byte)
                    : 0
            ) +
            (
                Flags.HasFlag(LinkInfoFlags.CreationOrderFieldIsPresent)
                    ? sizeof(ulong)
                    : 0
            ) +
            (
                Flags.HasFlag(LinkInfoFlags.LinkNameEncodingFieldIsPresent)
                    ? sizeof(byte)
                    : 0
            ) +
            (ushort)linkNameFieldLength +
            (ushort)encodedNameLength;
            LinkInfo.GetEncodeSize();
            
        return (ushort)size;
    }

    private ulong GetLinkNameFieldLength()
    {
        return (ulong)(1 << ((byte)Flags & 0x03));
    }
}
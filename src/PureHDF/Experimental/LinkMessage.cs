using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class LinkMessage
{
    public void Encode(BinaryWriter driver)
    {
        // version
        driver.Write(Version);

        // flags
        driver.Write((byte)Flags);

        // link type
        if (Flags.HasFlag(LinkInfoFlags.LinkTypeFieldIsPresent))
            driver.Write((byte)LinkType);

        // creation order
        if (Flags.HasFlag(LinkInfoFlags.CreatOrderFieldIsPresent))
            driver.Write(CreationOrder);

        // link name encoding
        if (Flags.HasFlag(LinkInfoFlags.LinkNameEncodingFieldIsPresent))
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // link length
        var encodedName = Encoding.UTF8.GetBytes(LinkName);
        var linkLengthFieldLength = (ulong)(1 << ((byte)Flags & 0x03));
        Utils.WriteUlongArbitrary(driver, (ulong)encodedName.Length, linkLengthFieldLength);

        // link name
        if (encodedName.Length > ushort.MaxValue)
            throw new Exception("The link name is too long.");

        driver.Write(encodedName);

        // link info
        LinkInfo.Encode(driver);
    }
}
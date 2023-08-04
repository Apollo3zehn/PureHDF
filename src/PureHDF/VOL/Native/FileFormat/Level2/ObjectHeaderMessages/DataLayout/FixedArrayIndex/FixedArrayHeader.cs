using System.Text;

namespace PureHDF.VOL.Native;

internal record class FixedArrayHeader(
    Superblock Superblock,
    ClientID ClientID,
    byte EntrySize,
    byte PageBits,
    ulong EntriesCount,
    ulong DataBlockAddress
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FAHD");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayHeader)} are supported.");

            _version = value;
        }
    }

    public static FixedArrayHeader Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientID = (ClientID)driver.ReadByte();

        // entry size
        var entrySize = driver.ReadByte();

        // page bits
        var pageBits = driver.ReadByte();

        // entries count
        var entriesCount = superblock.ReadLength(driver);

        // data block address
        var dataBlockAddress = superblock.ReadOffset(driver);

        // checksum
        var _ = driver.ReadUInt32();

        return new FixedArrayHeader(
            Superblock: superblock,
            ClientID: clientID,
            EntrySize: entrySize,
            PageBits: pageBits,
            EntriesCount: entriesCount,
            DataBlockAddress: dataBlockAddress
        )
        {
            Version = version
        };
    }
}
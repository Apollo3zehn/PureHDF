using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class FixedArrayHeader(
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

    public static async ValueTask<FixedArrayHeader> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientID = (ClientID)await driver.ReadByte().ConfigureAwait(false);

        // entry size
        var entrySize = await driver.ReadByte().ConfigureAwait(false);

        // page bits
        var pageBits = await driver.ReadByte().ConfigureAwait(false);

        // entries count
        var entriesCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // data block address
        var dataBlockAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

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
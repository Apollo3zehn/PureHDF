using System.Text;

namespace PureHDF.VOL.Native;

internal class FixedArrayHeader
{
    #region Fields

    private byte _version;
    private readonly Superblock _superblock;

    #endregion

    #region Constructors

    public FixedArrayHeader(NativeContext context)
    {
        var (driver, superblock) = context;

        _superblock = superblock;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, FixedArrayHeader.Signature);

        // version
        Version = driver.ReadByte();

        // client ID
        ClientID = (ClientID)driver.ReadByte();

        // entry size
        EntrySize = driver.ReadByte();

        // page bits
        PageBits = driver.ReadByte();

        // entries count
        EntriesCount = superblock.ReadLength(driver);

        // data block address
        DataBlockAddress = superblock.ReadOffset(driver);

        // checksum
        Checksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FAHD");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FixedArrayHeader)} are supported.");

            _version = value;
        }
    }

    public ClientID ClientID { get; }

    public byte EntrySize { get; }

    public byte PageBits { get; }

    public ulong EntriesCount { get; }

    public ulong DataBlockAddress { get; }

    public uint Checksum { get; }

    #endregion
}
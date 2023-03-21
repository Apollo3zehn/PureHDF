using System.Text;

namespace PureHDF.VOL.Native;

internal class FreeSpaceManagerHeader
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public FreeSpaceManagerHeader(NativeContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, FreeSpaceManagerHeader.Signature);

        // version
        Version = driver.ReadByte();

        // client ID
        ClientId = (ClientId)driver.ReadByte();

        // total space tracked
        TotalSpaceTracked = superblock.ReadLength(driver);

        // total sections count
        TotalSectionsCount = superblock.ReadLength(driver);

        // serialized sections count
        SerializedSectionsCount = superblock.ReadLength(driver);

        // un-serialized sections count
        UnSerializedSectionsCount = superblock.ReadLength(driver);

        // section classes count
        SectionClassesCount = driver.ReadUInt16();

        // shrink percent
        ShrinkPercent = driver.ReadUInt16();

        // expand percent
        ExpandPercent = driver.ReadUInt16();

        // address space size
        AddressSpaceSize = driver.ReadUInt16();

        // maximum section size
        MaximumSectionSize = superblock.ReadLength(driver);

        // serialized section list address
        SerializedSectionListAddress = superblock.ReadOffset(driver);

        // serialized section list used
        SerializedSectionListUsed = superblock.ReadLength(driver);

        // serialized section list allocated size
        SerializedSectionListAllocatedSize = superblock.ReadLength(driver);

        // checksum
        Checksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSHD");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceManagerHeader)} are supported.");

            _version = value;
        }
    }

    public ClientId ClientId { get; set; }
    public ulong TotalSpaceTracked { get; set; }
    public ulong TotalSectionsCount { get; set; }
    public ulong SerializedSectionsCount { get; set; }
    public ulong UnSerializedSectionsCount { get; set; }
    public ushort SectionClassesCount { get; set; }
    public ushort ShrinkPercent { get; set; }
    public ushort ExpandPercent { get; set; }
    public ushort AddressSpaceSize { get; set; }
    public ulong MaximumSectionSize { get; set; }
    public ulong SerializedSectionListAddress { get; set; }
    public ulong SerializedSectionListUsed { get; set; }
    public ulong SerializedSectionListAllocatedSize { get; set; }
    public uint Checksum { get; set; }

    #endregion
}
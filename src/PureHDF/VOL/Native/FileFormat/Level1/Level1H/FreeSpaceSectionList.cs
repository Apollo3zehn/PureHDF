using System.Text;

namespace PureHDF.VOL.Native;

internal class FreeSpaceSectionList
{
    #region Fields

    private NativeContext _context;
    private byte _version;

    #endregion

    #region Constructors

    public FreeSpaceSectionList(NativeContext context)
    {
        var (driver, superblock) = context;
        _context = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, FreeSpaceSectionList.Signature);

        // version
        Version = driver.ReadByte();

        // free space manager header address
        FreeSpaceManagerHeaderAddress = superblock.ReadOffset(driver);

        // TODO: implement everything

        // checksum
        Checksum = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSSE");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceSectionList)} are supported.");

            _version = value;
        }
    }

    public ulong FreeSpaceManagerHeaderAddress { get; set; }

    // TODO: implement everything
    // public List<ulong> SectionRecordsCount { get; set; }
    // public List<ulong> FreeSpaceSectionSize { get; set; }
    // public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
    // public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
    // public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>

    public uint Checksum { get; set; }

    public FreeSpaceManagerHeader FreeSpaceManagerHeader
    {
        get
        {
            _context.Driver.Seek((long)FreeSpaceManagerHeaderAddress, SeekOrigin.Begin);
            return new FreeSpaceManagerHeader(_context);
        }
    }

    #endregion
}
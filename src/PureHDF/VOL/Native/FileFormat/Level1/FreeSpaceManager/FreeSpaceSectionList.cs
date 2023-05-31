using System.Text;

namespace PureHDF.VOL.Native;

// TODO: implement everything
// public List<ulong> SectionRecordsCount { get; set; }
// public List<ulong> FreeSpaceSectionSize { get; set; }
// public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
// public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
// public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>

internal record struct FreeSpaceSectionList(
    NativeContext Context,
    ulong FreeSpaceManagerHeaderAddress
)
{
    private FreeSpaceManagerHeader? _freeSpaceManagerHeader;

    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSSE");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceSectionList)} are supported.");

            _version = value;
        }
    }

    public FreeSpaceManagerHeader FreeSpaceManagerHeader
    {
        get
        {
            if (_freeSpaceManagerHeader is null)
            {
                Context.Driver.Seek((long)FreeSpaceManagerHeaderAddress, SeekOrigin.Begin);
                _freeSpaceManagerHeader = FreeSpaceManagerHeader.Decode(Context);
            };
            
            return _freeSpaceManagerHeader;
        }
    }

    public static FreeSpaceSectionList Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // free space manager header address
        var freeSpaceManagerHeaderAddress = superblock.ReadOffset(driver);

        // TODO: implement everything

        // checksum
        var _ = driver.ReadUInt32();

        return new FreeSpaceSectionList(
            Context: context,
            FreeSpaceManagerHeaderAddress: freeSpaceManagerHeaderAddress
        )
        {
            Version = version
        };
    }
}
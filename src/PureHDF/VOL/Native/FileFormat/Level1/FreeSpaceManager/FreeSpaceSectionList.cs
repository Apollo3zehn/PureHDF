using System.Text;

namespace PureHDF.VOL.Native;

// TODO: implement everything
// public List<ulong> SectionRecordsCount { get; set; }
// public List<ulong> FreeSpaceSectionSize { get; set; }
// public List<ulong> SectionRecordOffset { get; set; } // actually it is a List<List<ulong>>
// public List<ulong> SectionRecordType { get; set; } // actually it is a List<List<SectionType>>
// public List<SectionDataRecord> SectionRecordData { get; set; } // actually it is a List<List<SectionDataRecord>>

internal record struct FreeSpaceSectionList(
    NativeReadContext Context,
    ulong FreeSpaceManagerHeaderAddress
)
{
    private FreeSpaceManagerHeader? _freeSpaceManagerHeader;

    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSSE");

    public required byte Version
    {
        readonly get
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

    public async ValueTask<FreeSpaceManagerHeader> GetFreeSpaceManagerHeader()
    {
        if (_freeSpaceManagerHeader is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)FreeSpaceManagerHeaderAddress);
            _freeSpaceManagerHeader = await FreeSpaceManagerHeader.Decode(Context).ConfigureAwait(false);
        };

        return _freeSpaceManagerHeader;
    }

    public static async ValueTask<FreeSpaceSectionList> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // free space manager header address
        var freeSpaceManagerHeaderAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // TODO: implement everything

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new FreeSpaceSectionList(
            Context: context,
            FreeSpaceManagerHeaderAddress: freeSpaceManagerHeaderAddress
        )
        {
            Version = version
        };
    }
}
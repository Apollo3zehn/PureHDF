using System.Text;

namespace PureHDF.VOL.Native;

// TODO: Implement this.
// public byte[] ObjectData { get; set; }

internal record class FractalHeapDirectBlock(
    NativeReadContext Context,
    ulong HeapHeaderAddress,
    ulong BlockOffset,
    ulong HeaderSize
)
{
    private byte _version;
    private FractalHeapHeader? _header;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHDB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapDirectBlock)} are supported.");

            _version = value;
        }
    }

    public async ValueTask<FractalHeapHeader> GetHeapHeader()
    {
        if (_header is null)
        {
            Context.Driver.SeekRelativeToBaseAddress((long)HeapHeaderAddress);
            _header = await FractalHeapHeader.Decode(Context).ConfigureAwait(false);
        }

        return _header;
    }

    public static async ValueTask<FractalHeapDirectBlock> Decode(NativeReadContext context, FractalHeapHeader header)
    {
        var (driver, superblock) = context;

        var headerSize = 0UL;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        headerSize += 4;
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);
        headerSize += 1;

        // heap header address
        var heapHeaderAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);
        headerSize += superblock.OffsetsSize;

        // block offset
        var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
        var blockOffset = await ReadUtils.ReadUlong(driver, (ulong)blockOffsetFieldSize).ConfigureAwait(false);
        headerSize += (ulong)blockOffsetFieldSize;

        // checksum
        if (header.Flags.HasFlag(FractalHeapHeaderFlags.DirectBlocksAreChecksummed))
        {
            var _ = await driver.ReadUInt32().ConfigureAwait(false);
            headerSize += 4;
        }

        return new FractalHeapDirectBlock(
            Context: context,
            HeapHeaderAddress: heapHeaderAddress,
            BlockOffset: blockOffset,
            HeaderSize: headerSize
        )
        {
            Version = version
        };
    }
}
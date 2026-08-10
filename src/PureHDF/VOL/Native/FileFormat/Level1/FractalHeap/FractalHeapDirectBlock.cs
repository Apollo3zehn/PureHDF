using System.Text;

namespace PureHDF.VOL.Native;

// TODO: Implement this.
// public byte[] ObjectData { get; set; }

// CONCURRENCY: holds no NativeReadContext. It is transient - decoded inside FractalHeapHeader's
// GetAddress/Locate and discarded - so a capture would have been survivable, but the ONLY thing that
// needed one was a lazy GetHeapHeader() with no callers anywhere in the tree, so both are gone.
internal sealed record class FractalHeapDirectBlock(
    ulong HeapHeaderAddress,
    ulong BlockOffset,
    ulong HeaderSize
)
{
    private byte _version;

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
            HeapHeaderAddress: heapHeaderAddress,
            BlockOffset: blockOffset,
            HeaderSize: headerSize
        )
        {
            Version = version
        };
    }
}
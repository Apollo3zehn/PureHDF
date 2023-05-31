using System.Text;

namespace PureHDF.VOL.Native;

// TODO: Implement this.
// public byte[] ObjectData { get; set; }

internal record class FractalHeapDirectBlock(
    NativeContext Context,
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

    public FractalHeapHeader HeapHeader
    {
        get
        {
            if (_header is null)
            {
                Context.Driver.Seek((long)HeapHeaderAddress, SeekOrigin.Begin);
                _header = FractalHeapHeader.Decode(Context);
            }

            return _header;
        }
    }

    public static FractalHeapDirectBlock Decode(NativeContext context, FractalHeapHeader header)
    {
        var (driver, superblock) = context;

        var headerSize = 0UL;

        // signature
        var signature = driver.ReadBytes(4);
        headerSize += 4;
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();
        headerSize += 1;

        // heap header address
        var heapHeaderAddress = superblock.ReadOffset(driver);
        headerSize += superblock.OffsetsSize;

        // block offset
        var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
        var blockOffset = Utils.ReadUlong(driver, (ulong)blockOffsetFieldSize);
        headerSize += (ulong)blockOffsetFieldSize;

        // checksum
        if (header.Flags.HasFlag(FractalHeapHeaderFlags.DirectBlocksAreChecksummed))
        {
            var _ = driver.ReadUInt32();
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
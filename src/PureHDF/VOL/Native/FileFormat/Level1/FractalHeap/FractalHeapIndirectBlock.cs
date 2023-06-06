using System.Text;

namespace PureHDF.VOL.Native;

internal record class FractalHeapIndirectBlock(
    NativeContext Context,
    ulong HeapHeaderAddress,
    ulong BlockOffset,
    FractalHeapEntry[] Entries,
    uint RowCount,
    uint ChildCount,
    uint MaxChildIndex
)
{
    private byte _version;
    private FractalHeapHeader? _header;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHIB");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapIndirectBlock)} are supported.");

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

        public static FractalHeapIndirectBlock Decode(
        NativeContext context, 
        FractalHeapHeader header, 
        uint rowCount)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // heap header address
        var heapHeaderAddress = superblock.ReadOffset(driver);

        // block offset
        var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
        var blockOffset = Utils.ReadUlong(driver, (ulong)blockOffsetFieldSize);

        // H5HFcache.c (H5HF__cache_iblock_deserialize)
        var length = rowCount * header.TableWidth;
        var entries = new FractalHeapEntry[length];

        var childCount = default(uint);
        var maxChildIndex = default(uint);

        for (uint i = 0; i < entries.Length; i++)
        {
            /* Decode child block address */
            var address = superblock.ReadOffset(driver);

            /* Check for heap with I/O filters */
            var filteredSize = default(ulong);
            var filterMask = default(uint);

            if (header.IOFilterEncodedLength > 0)
            {
                /* Decode extra information for direct blocks */
                if (i < (header.MaxDirectRows * header.TableWidth))
                {
                    /* Size of filtered direct block */
                    filteredSize = superblock.ReadLength(driver);

                    /* I/O filter mask for filtered direct block */
                    filterMask = driver.ReadUInt32();
                }
            }

            entries[i] = new FractalHeapEntry(
                Address: address,
                FilteredSize: filteredSize,
                FilterMask: filterMask
            );

            /* Count child blocks */
            if (!superblock.IsUndefinedAddress(entries[i].Address))
            {
                childCount++;
                maxChildIndex = i;
            }
        }

        // checksum
        var _ = driver.ReadUInt32();

        return new FractalHeapIndirectBlock(
            Context: context,
            HeapHeaderAddress: heapHeaderAddress,
            BlockOffset: blockOffset,
            Entries: entries,
            RowCount: rowCount,
            ChildCount: childCount,
            MaxChildIndex: maxChildIndex
        )
        {
            Version = version
        };
    }
}
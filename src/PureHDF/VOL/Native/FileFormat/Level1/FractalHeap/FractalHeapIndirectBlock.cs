using System.Text;

namespace PureHDF.VOL.Native;

// CONCURRENCY: holds no NativeReadContext. It is transient — decoded inside FractalHeapHeader's
// GetAddress/Locate and discarded — so it has no need for a context: the only member that would have
// used one (a lazy GetHeapHeader()) has no callers anywhere in the tree.
internal sealed record class FractalHeapIndirectBlock(
    ulong HeapHeaderAddress,
    ulong BlockOffset,
    FractalHeapEntry[] Entries,
    uint RowCount,
    uint ChildCount,
    uint MaxChildIndex
)
{
    private byte _version;

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

    public static async ValueTask<FractalHeapIndirectBlock> Decode(
    NativeReadContext context,
    FractalHeapHeader header,
    uint rowCount)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // heap header address
        var heapHeaderAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // block offset
        var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
        var blockOffset = await ReadUtils.ReadUlong(driver, (ulong)blockOffsetFieldSize).ConfigureAwait(false);

        // H5HFcache.c (H5HF__cache_iblock_deserialize)
        var length = rowCount * header.TableWidth;
        var entries = new FractalHeapEntry[length];

        var childCount = default(uint);
        var maxChildIndex = default(uint);

        for (uint i = 0; i < entries.Length; i++)
        {
            /* Decode child block address */
            var address = await superblock.ReadOffset(driver).ConfigureAwait(false);

            /* Check for heap with I/O filters */
            var filteredSize = default(ulong);
            var filterMask = default(uint);

            if (header.IOFilterEncodedLength > 0)
            {
                /* Decode extra information for direct blocks */
                if (i < (header.MaxDirectRows * header.TableWidth))
                {
                    /* Size of filtered direct block */
                    filteredSize = await superblock.ReadLength(driver).ConfigureAwait(false);

                    /* I/O filter mask for filtered direct block */
                    filterMask = await driver.ReadUInt32().ConfigureAwait(false);
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
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

        return new FractalHeapIndirectBlock(
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
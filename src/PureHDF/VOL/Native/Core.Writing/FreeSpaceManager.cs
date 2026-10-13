namespace PureHDF.VOL.Native;

/// <summary>
/// What an allocation holds, which is what lets the allocator keep structure and payload apart.
/// </summary>
/// <remarks>
/// The distinction is the reader's, not the writer's: a range-request reader walking a file's structure
/// needs every byte of metadata and none of the raw data, so metadata scattered through the file costs
/// it the whole file. See <see cref="H5MetadataPlacement" />.
/// </remarks>
internal enum AllocationKind
{
    /// <summary>
    /// File structure: the superblock, object headers, chunk indexes and their data blocks, and the
    /// global heap collections holding attribute values - which a viewer reads while browsing a file,
    /// rather than while reading a dataset - or a compact dataset's variable-length values, since its
    /// data is part of its header.
    /// </summary>
    Metadata,

    /// <summary>
    /// Dataset payload written at the address it was allocated: contiguous data and chunk data.
    /// </summary>
    RawData,

    /// <summary>
    /// Dataset payload allocated now and written later - the global heap collections holding a
    /// non-compact dataset's own variable-length elements, which are filled progressively and flushed
    /// in batches.
    /// </summary>
    /// <remarks>
    /// Payload, so it stays out of the metadata region: counted as structure, it makes up 97% of a
    /// variable-length string dataset and leaves a placement nothing to separate. But clustered
    /// rather than bump-allocated, because the gap between allocating and writing is what costs a
    /// streaming writer. A writer uploading to object storage buffers the file in fixed parts and can
    /// only ship a part once every byte in it is written, so a region allocated now and written much
    /// later is a hole that pins its part. Scattered through the file, those holes pin every part and
    /// the whole object stays in memory; gathered into a few blocks, they pin a few.
    /// <para>
    /// Chunk and contiguous payload needs none of this - it is written at the address it was allocated,
    /// so it leaves no hole behind.
    /// </para>
    /// </remarks>
    DeferredRawData
}

internal class FreeSpaceManager
{
    /// <summary>
    /// The size of the first metadata block. Blocks double from here up to the configured maximum.
    /// </summary>
    /// <remarks>
    /// A block is claimed in full, so a fixed size is paid in full however little metadata the file
    /// turns out to have: at a fixed 8 MB, 33 kB of metadata makes an 8.4 MB file. Doubling bounds the
    /// total claimed at roughly twice what is used, which is a proportional cost rather than a floor,
    /// while a file with enough metadata still reaches the maximum after a handful of blocks and clusters
    /// at full block size from then on.
    /// </remarks>
    private const long INITIAL_BLOCK_SIZE = 64 * 1024;

    /// <summary>
    /// Where payload is placed while the structure before it is still growing - see
    /// <see cref="ReserveProvisionalRegion" />. Far beyond any real file, so an address at or past it is
    /// unambiguously provisional.
    /// </summary>
    public const long PROVISIONAL_PAYLOAD_BASE = 1L << 48;

    private readonly H5MetadataPlacement _placement;
    private readonly long _blockSize;

    private long _length;
    private long _abandonedByReplacement;
    private long _payloadAbandonedByReplacement;

    // The region currently being filled for each kind that gets one: [Cursor, End). Empty when the two
    // are equal, which is always the case for H5MetadataPlacement.Interleaved. Metadata and deferred
    // payload are clustered independently - they must not share a block, or payload would land inside
    // the region a reader fetches to walk the structure.
    private Region _metadata;
    private Region _deferredRawData;

    // How far provisional payload moved once the structure was placed; zero before then.
    private long _provisionalShift;
    private bool _isProvisional;

    public FreeSpaceManager(H5MetadataPlacement placement = H5MetadataPlacement.Interleaved, long blockSize = 0)
    {
        _placement = placement;
        _blockSize = blockSize;
        _metadata = new Region { NextBlockSize = Math.Min(INITIAL_BLOCK_SIZE, blockSize) };
        _deferredRawData = new Region { NextBlockSize = Math.Min(INITIAL_BLOCK_SIZE, blockSize) };
    }

    private struct Region
    {
        public long Cursor;
        public long End;
        public long NextBlockSize;
    }

    /// <summary>
    /// The first byte past everything allocated so far.
    /// </summary>
    /// <remarks>
    /// Not the same as the stream length once a metadata region is in play: the tail of a region that
    /// nothing was written into is allocated but never touched, so the stream can be shorter than this.
    /// The end-of-file address in the superblock must cover it regardless, or a reader is entitled to
    /// treat addresses beyond the declared end as invalid.
    /// </remarks>
    public long HighWaterMark => _length;

    /// <summary>
    /// Total bytes handed out for <see cref="AllocationKind.Metadata" />.
    /// </summary>
    /// <remarks>
    /// A count of ALLOCATIONS rather than of encoded bytes: a global heap collection is allocated at a
    /// 4 kB minimum and usually left partly empty, so the space structure takes exceeds the space that
    /// ends up meaning anything. h5stat reports that difference as unaccounted space rather than as
    /// metadata, which is what makes its "File metadata" figure too small to reserve against.
    /// </remarks>
    public long MetadataAllocated { get; private set; }

    /// <summary>
    /// How many metadata regions were opened. More than one under
    /// <see cref="H5MetadataPlacement.FrontLoaded" /> means the reservation was too small and the
    /// remainder spilled.
    /// </summary>
    public int MetadataRegionsOpened { get; private set; }

    /// <summary>
    /// How much of those regions was claimed and never used.
    /// </summary>
    /// <remarks>
    /// Includes the tail of the region still open, which is the whole point: that tail is the largest
    /// part of the waste on a file that opened one region and half filled it - counting only replaced
    /// regions would report 48 bytes where 8 MB had been claimed. A region is replaced only when a
    /// request does not fit, so the remainders counted on replacement are small by construction, and
    /// the open one is not.
    /// <para>
    /// Live rather than final while a write is in progress: the open region's tail shrinks as
    /// allocations are served from it. While a front-loaded write holds its structure, the open region
    /// runs to <see cref="PROVISIONAL_PAYLOAD_BASE" />, so the figure means something only once
    /// <see cref="PlaceProvisionalRegion" /> has closed it.
    /// </para>
    /// </remarks>
    public long MetadataAbandoned => _abandonedByReplacement + (_metadata.End - _metadata.Cursor);

    /// <summary>
    /// The same, for the blocks holding <see cref="AllocationKind.DeferredRawData" />.
    /// </summary>
    /// <remarks>
    /// Reported separately because it is payload: it is not what a reservation should be sized against,
    /// and it is not what <see cref="MetadataRegionsOpened" /> counts. Together with
    /// <see cref="MetadataAbandoned" /> it accounts for every byte a clustered file carries that an
    /// interleaved one does not.
    /// </remarks>
    public long PayloadAbandoned => _payloadAbandonedByReplacement + (_deferredRawData.End - _deferredRawData.Cursor);

    /// <summary>
    /// Allocates metadata at the current end of the file, bypassing regions and blocks entirely.
    /// </summary>
    /// <remarks>
    /// Exists for the superblock, which must land at offset zero. It cannot go through
    /// <see cref="Allocate" />: with blocks enabled the first metadata request opens a block, so routing
    /// the superblock there gives it a whole block to itself, ahead of the region - which costs that
    /// block of file size and pushes the region away from the front, the one thing the front-loaded
    /// placement exists to avoid.
    /// </remarks>
    public long AllocateAtFront(long length)
    {
        MetadataAllocated += length;

        var address = _length;
        _length += length;

        return address;
    }

    /// <summary>
    /// Reserves <paramref name="size" /> bytes at the current end of the file for metadata.
    /// </summary>
    /// <remarks>
    /// Called once, immediately after the superblock is allocated, so the region begins as close to the
    /// front of the file as it can. Metadata is served from here until it is exhausted; raw data is
    /// always served past the end of the file, so it never lands inside the region.
    /// </remarks>
    public void ReserveMetadataRegion(long size)
    {
        if (size <= 0)
            return;

        _metadata.Cursor = _length;
        _metadata.End = _length + size;
        _length = _metadata.End;
        MetadataRegionsOpened++;
    }

    /// <summary>
    /// Reserves everything from here up to <see cref="PROVISIONAL_PAYLOAD_BASE" /> for metadata, and
    /// places payload from there, until <see cref="PlaceProvisionalRegion" /> shrinks the region to
    /// what was used and moves the payload down behind it.
    /// </summary>
    /// <remarks>
    /// Front loading puts all structure before all payload, so a payload address depends on how large
    /// the structure turns out to be. Placing payload provisionally lets the structure be encoded once,
    /// as it is, rather than measured by encoding it a first time. The price is that every provisional
    /// address the structure names has to be corrected before it is written, which is
    /// <see cref="Relocate" />.
    /// </remarks>
    public void ReserveProvisionalRegion()
    {
        ReserveMetadataRegion(PROVISIONAL_PAYLOAD_BASE - _length);
        _isProvisional = true;
    }

    /// <summary>
    /// Ends the region <see cref="ReserveProvisionalRegion" /> opened where its metadata ends, and moves
    /// the payload placed so far to start there.
    /// </summary>
    /// <returns>Where the structure ends, which is where the payload now begins.</returns>
    public long PlaceProvisionalRegion()
    {
        if (!_isProvisional)
            throw new InvalidOperationException("No provisional region is open.");

        var structureEnd = _metadata.Cursor;

        _provisionalShift = PROVISIONAL_PAYLOAD_BASE - structureEnd;
        _metadata.End = structureEnd;
        _length -= _provisionalShift;

        // A payload block opened provisionally moves with the payload in it.
        if (_deferredRawData.End >= PROVISIONAL_PAYLOAD_BASE)
        {
            _deferredRawData.Cursor -= _provisionalShift;
            _deferredRawData.End -= _provisionalShift;
        }

        _isProvisional = false;

        return structureEnd;
    }

    /// <summary>
    /// Whether <paramref name="address" /> is payload placed provisionally and not yet moved.
    /// </summary>
    public bool IsProvisional(ulong address)
        => _isProvisional && address >= PROVISIONAL_PAYLOAD_BASE && address != Superblock.UndefinedAddress;

    /// <summary>
    /// Where a provisionally placed address ended up; any other address is returned unchanged, so this
    /// can be applied to an address more than once.
    /// </summary>
    public ulong Relocate(ulong address)
        => address >= PROVISIONAL_PAYLOAD_BASE && address != Superblock.UndefinedAddress
            ? address - (ulong)_provisionalShift
            : address;

    public long Allocate(long length, AllocationKind kind)
    {
        if (length == 0)
            return Superblock.LongUndefinedAddress;

        if (kind == AllocationKind.Metadata)
        {
            MetadataAllocated += length;

            if (TryServeClustered(ref _metadata, length, out var metadataAddress, out var abandoned))
            {
                if (abandoned >= 0)
                {
                    _abandonedByReplacement += abandoned;
                    MetadataRegionsOpened++;
                }

                return metadataAddress;
            }
        }

        // Payload that is written long after it is allocated, so it is gathered rather than scattered -
        // see AllocationKind.DeferredRawData. Its own blocks, never the metadata region: a reader
        // walking the structure must not have to fetch this.
        else if (kind == AllocationKind.DeferredRawData)
        {
            // Counted apart from the structure region's figures, which is what PayloadAbandoned is for.
            if (TryServeClustered(ref _deferredRawData, length, out var deferredAddress, out var payloadAbandoned))
            {
                if (payloadAbandoned >= 0)
                    _payloadAbandonedByReplacement += payloadAbandoned;

                return deferredAddress;
            }
        }

        var bumped = _length;
        _length += length;

        return bumped;
    }

    /// <summary>
    /// Serves <paramref name="length" /> from <paramref name="region" />, opening a fresh block when the
    /// current one is exhausted. False when blocks are disabled or the request is too large for one, in
    /// which case the caller bump-allocates instead.
    /// </summary>
    /// <remarks>
    /// Whatever is left of a replaced region is abandoned - there is no free list to return it to - so
    /// this trades a bounded amount of file size for locality. The waste is at most one request's worth
    /// per region, since a region is only replaced when the request does not fit, plus the unused tail
    /// of the final region.
    /// </remarks>
    private bool TryServeClustered(ref Region region, long length, out long address, out long abandoned)
    {
        // Serve from the open region while it has room.
        if (region.Cursor + length <= region.End)
        {
            address = region.Cursor;
            region.Cursor += length;
            abandoned = -1;

            return true;
        }

        // Exhausted, or never opened. Opening a fresh one clusters what follows instead of scattering
        // it. FrontLoaded does this too rather than failing: a reservation that came out short degrades
        // to Aggregated behaviour for the remainder, which is worse for locality but still correct and
        // still far better than interleaving.
        if (_blockSize > 0 && length <= _blockSize)
        {
            // Big enough for the request that opened it, since a block too small to serve that request
            // would be abandoned immediately and the request placed inline.
            var openedSize = Math.Max(region.NextBlockSize, length);

            abandoned = region.End - region.Cursor;

            region.Cursor = _length;
            region.End = _length + openedSize;
            _length = region.End;
            region.NextBlockSize = Math.Min(openedSize * 2, _blockSize);

            address = region.Cursor;
            region.Cursor += length;

            return true;
        }

        address = default;
        abandoned = -1;

        return false;
    }
}

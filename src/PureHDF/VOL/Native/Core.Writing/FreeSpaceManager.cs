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
    /// File structure: the superblock, object headers, chunk indexes and their data blocks, and global
    /// heap collections. Global heap counts as metadata because it holds attribute payload, which is
    /// what a viewer reads while browsing rather than while reading a dataset.
    /// </summary>
    Metadata,

    /// <summary>
    /// Dataset payload: contiguous data and chunk data.
    /// </summary>
    RawData
}

internal class FreeSpaceManager
{
    private readonly H5MetadataPlacement _placement;
    private readonly long _blockSize;

    private long _length;

    // The metadata region currently being filled: [_metadataCursor, _metadataRegionEnd). Empty when
    // the two are equal, which is always the case for H5MetadataPlacement.Interleaved.
    private long _metadataCursor;
    private long _metadataRegionEnd;

    public FreeSpaceManager(H5MetadataPlacement placement = H5MetadataPlacement.Interleaved, long blockSize = 0)
    {
        _placement = placement;
        _blockSize = blockSize;
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
    /// This is what a sizing pass reads off to decide how much to reserve, and why it is a count of
    /// ALLOCATIONS rather than of encoded bytes: a global heap collection is allocated at a 4 kB minimum
    /// and usually left partly empty, so the space that has to be reserved exceeds the space that ends up
    /// meaning anything. h5stat reports that difference as unaccounted space rather than as metadata,
    /// which is what makes its "File metadata" figure too small to reserve against.
    /// </remarks>
    public long MetadataAllocated { get; private set; }

    /// <summary>
    /// How many metadata regions were opened, and how much of them was abandoned unused. More than one
    /// region under <see cref="H5MetadataPlacement.FrontLoaded" /> means the reservation was too small
    /// and the remainder spilled.
    /// </summary>
    public int MetadataRegionsOpened { get; private set; }

    public long MetadataAbandoned { get; private set; }

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

        _metadataCursor = _length;
        _metadataRegionEnd = _length + size;
        _length = _metadataRegionEnd;
        MetadataRegionsOpened++;
    }

    public long Allocate(long length, AllocationKind kind)
    {
        if (length == 0)
            return Superblock.LongUndefinedAddress;

        if (kind == AllocationKind.Metadata)
        {
            MetadataAllocated += length;

            // Serve from the open region while it has room.
            if (_metadataCursor + length <= _metadataRegionEnd)
            {
                var reserved = _metadataCursor;
                _metadataCursor += length;

                return reserved;
            }

            // The region is exhausted (or was never opened). Opening a fresh one clusters what follows
            // instead of scattering it. FrontLoaded does this too rather than failing: an estimate that
            // came out short degrades to Aggregated behaviour for the remainder, which is worse for
            // locality but still correct and still far better than interleaving.
            //
            // Whatever is left of the old region is abandoned - there is no free list to return it to -
            // so this trades a bounded amount of file size for locality. The waste is at most one
            // request's worth per region, since a region is only replaced when the request does not
            // fit, plus the unused tail of the final region.
            if (_blockSize > 0 && length <= _blockSize)
            {
                MetadataAbandoned += _metadataRegionEnd - _metadataCursor;
                MetadataRegionsOpened++;

                _metadataCursor = _length;
                _metadataRegionEnd = _length + _blockSize;
                _length = _metadataRegionEnd;

                var address = _metadataCursor;
                _metadataCursor += length;

                return address;
            }
        }

        // Raw data, metadata too large to fit a block, and everything at all when the placement is
        // Interleaved: straight bump allocation at the end of the file, with no region or block involved.
        var bumped = _length;
        _length += length;

        return bumped;
    }
}

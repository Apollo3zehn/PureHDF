namespace PureHDF;

/// <summary>
/// Controls where the writer places file structure relative to dataset payload.
/// </summary>
/// <remarks>
/// This matters only to a reader that fetches the file in ranges rather than mapping it - typically over
/// HTTP. Such a reader must read every byte of structure to walk the file, so structure spread evenly
/// through the file costs it the entire file. On a 620 MB file whose structure is 8.2% of its bytes,
/// walking that structure touched 591 of 591 one-megabyte ranges.
/// <para>
/// The file is valid HDF5 under every setting - the format imposes no ordering - so this is purely a
/// layout hint and costs nothing on read for a local file.
/// </para>
/// </remarks>
public enum H5MetadataPlacement
{
    /// <summary>
    /// Structure and payload are allocated in the order they are encoded, so they interleave.
    /// </summary>
    /// <remarks>
    /// The default, and the cheapest placement in both file size and write time - the only one that adds
    /// nothing at all to a write.
    /// </remarks>
    Interleaved = 0,

    /// <summary>
    /// Structure is allocated from blocks, so it forms a few clusters rather than being spread evenly.
    /// Needs no estimate of the total.
    /// </summary>
    /// <remarks>
    /// The equivalent of the HDF5 C library's <c>H5Pset_meta_block_size</c>. Use this when the shape of
    /// the file is not known ahead of time.
    /// <para>
    /// Blocks start small and double up to <see cref="H5WriteOptions.MetadataBlockSize" />, so the space
    /// claimed is proportional to the metadata the file turns out to have rather than a floor it pays
    /// whatever its size. The cost is a handful of clusters rather than one: measured over a
    /// range-fetching reader on a 4 MB file of 600 groups, walking the structure took 3 requests and
    /// 19.4% of the file, against 16 requests and all of it under <see cref="Interleaved" />. Prefer
    /// <see cref="FrontLoaded" /> where the round trips matter - it took 1 - and this where the sizing
    /// pass is unwelcome.
    /// </para>
    /// </remarks>
    Aggregated = 1,

    /// <summary>
    /// Structure is allocated from a single region reserved at the front of the file, so a reader can
    /// fetch all of it in one range.
    /// </summary>
    /// <remarks>
    /// The region is sized from <see cref="H5WriteOptions.MetadataReservation" /> when that is set, and
    /// otherwise by measuring: the writer encodes the file once against a stream that discards
    /// everything and reads the total off its allocator. That figure is exact, since it comes from the
    /// same encoder and the same allocator, which is what lets it cover a chunk index's dependence on
    /// chunk count and the global heap's 4 kB collection granularity - neither of which an estimate can
    /// see, and both of which <c>h5stat</c> excludes from its "File metadata" figure.
    /// <para>
    /// A reservation too small for the file places the remainder as <see cref="Aggregated" /> would, which
    /// loses locality rather than failing. One too large wastes the unused tail, which <c>h5stat</c>
    /// reports as unaccounted space. A measured reservation is neither: it is the exact number of bytes
    /// that will be served from it, so a front-loaded file is the same size as an interleaved one rather
    /// than merely close to it, and nothing is abandoned.
    /// A deferred write through <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> needs no
    /// allowance for this: nothing a caller writes later changes how much structure the file has.
    /// </para>
    /// <para>
    /// Suited to a file written once and then only read - which is every file this library produces, as
    /// the writer cannot add structure to an existing file at all. Prefer <see cref="Aggregated" /> where
    /// the sizing pass is unwelcome and one range request per cluster is good enough.
    /// </para>
    /// </remarks>
    FrontLoaded = 2
}

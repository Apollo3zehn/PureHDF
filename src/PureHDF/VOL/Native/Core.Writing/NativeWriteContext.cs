namespace PureHDF.VOL.Native;

/// <summary>
/// Identifies a cached datatype message. A <see cref="Type"/> alone is not enough: a string's
/// width and an opaque type's size and tag are supplied by the caller, so one type maps to many
/// messages. Everything that varies is part of the key, which is what makes the cache safe to
/// read for those types rather than having to skip them.
/// </summary>
/// <param name="Type">The .NET type being encoded.</param>
/// <param name="StringLength">The requested fixed-width string length; 0 means variable-length.</param>
/// <param name="OpaqueInfo">The opaque size and tag, when encoding an opaque type.</param>
internal readonly record struct DatatypeCacheKey(
    Type Type,
    int StringLength,
    H5OpaqueInfo? OpaqueInfo);

internal record NativeWriteContext(
    H5NativeWriter Writer,
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Dataset, (H5D_Base H5D, object Encode)> DatasetToInfoMap,
    Dictionary<DatasetInfo, (long ObjectHeaderStart, int ObjectHeaderLength)> DatasetInfoToObjectHeaderMap,
    Dictionary<DatatypeCacheKey, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<H5Object, ulong> ObjectToAddressMap,
    Dictionary<H5Object, int> ObjectReferenceCountMap,
    Dictionary<object, H5Dataset> RawValueToDatasetMap,
    SystemMemoryStream ShortlivedStream
)
{
    /// <summary>
    /// Set for the pass whose only purpose is to total up how much structure the file needs, so that a
    /// front-loaded reservation can be sized exactly instead of estimated.
    /// </summary>
    /// <remarks>
    /// Everything that decides a SIZE still runs: every object is encoded, every chunk is enumerated and
    /// allocated, every heap collection is opened. What is skipped is compressing chunk payload, because
    /// no metadata size depends on the compressed result - a filtered chunk index entry's size field is
    /// sized from <c>ChunkByteSize</c>, the uncompressed chunk size - and compression is around 97% of a
    /// filtered write, so running it would make the pass cost as much as the file it is measuring.
    /// </remarks>
    public bool SizeOnly { get; init; }
}
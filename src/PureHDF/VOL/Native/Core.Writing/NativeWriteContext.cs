namespace PureHDF.VOL.Native;

/// <summary>
/// Identifies a cached datatype message. A <see cref="Type"/> alone is not enough: a string's
/// width and an opaque type's size and tag are supplied by the caller, so one type maps to many
/// messages. Everything that varies is part of the key, which is what makes the cache safe to
/// read for those types rather than having to skip them.
/// </summary>
/// <param name="Type">The .NET type being encoded.</param>
/// <param name="StringLength">The requested fixed-width string length; 0 means variable-length.</param>
/// <param name="StringPadding">
/// How a fixed-width string declares its unused bytes. Part of the key because two callers can ask
/// for the same width and mean different things by it: a width measured from a value fills the field
/// exactly and pads with nothing, while a declared width leaves room to terminate. Leaving it out
/// would let whichever is encoded first hand its padding to the other.
/// </param>
/// <param name="OpaqueInfo">The opaque size and tag, when encoding an opaque type.</param>
internal readonly record struct DatatypeCacheKey(
    Type Type,
    int StringLength,
    PaddingType StringPadding,
    H5OpaqueInfo? OpaqueInfo);

/// <summary>
/// What the writer keeps per dataset so that its data can be written after its object header.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="H5D" />, <see cref="Info" /> and <see cref="Contiguous" /> is set:
/// <list type="bullet">
/// <item><see cref="H5D" /> for a chunked dataset, whose layout carries state from encoding to
/// <see cref="H5NativeWriter.Dispose()" /> (its chunk cache and the index it allocates then).</item>
/// <item><see cref="Contiguous" /> for a contiguous one, which needs no more than where its data goes
/// and what it looks like. Everything else is built again for each write.</item>
/// <item><see cref="Info" /> for anything else (compact, null dataspace), rare enough not to matter.</item>
/// </list>
/// A file of many small datasets is almost all contiguous ones, and for those, holding the full
/// layout and messages instead was most of what the writer kept per dataset.
/// </remarks>
internal readonly record struct DeferredDataset(
    H5D_Base? H5D,
    DatasetInfo? Info,
    ContiguousTarget? Contiguous,
    object Encode)
{
    public DatasetInfo GetInfo() => Info ?? Contiguous?.ToDatasetInfo()
        ?? throw new InvalidOperationException("A deferred dataset with a layout has no dataset info.");
}

/// <summary>
/// The minimum to write a contiguous dataset's data: its dimensions and datatype, and where its data
/// goes.
/// </summary>
internal sealed record class ContiguousTarget(ulong[] FileDims, DatatypeMessage Type, ulong Address, ulong Size)
{
    /// <summary>
    /// Settable so that a provisionally placed address can be moved to where the payload ended up. See
    /// <see cref="FreeSpaceManager.ReserveProvisionalRegion" />.
    /// </summary>
    public ulong Address { get; set; } = Address;

    public DatasetInfo ToDatasetInfo() => new(
        Space: DataspaceMessage.Create(FileDims),
        Type: Type,
        Layout: new DataLayoutMessage4(
            LayoutClass: LayoutClass.Contiguous,
            Properties: new ContiguousStoragePropertyDescription(Size: Size) { Address = Address })
        {
            Version = 4
        },
        FillValue: H5NativeWriter.DefaultFillValueMessage,
        FilterPipeline: default,
        ExternalFileList: default);
}

internal record NativeWriteContext(
    H5NativeWriter Writer,
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Dataset, DeferredDataset> DatasetToInfoMap,
    Dictionary<DatasetInfo, (long ObjectHeaderAddress, byte[] ObjectHeader)> DatasetInfoToObjectHeaderMap,
    Dictionary<DatatypeCacheKey, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<H5Object, ulong> ObjectToAddressMap,
    Dictionary<H5Object, int> ObjectReferenceCountMap,
    Dictionary<object, H5Dataset> RawValueToDatasetMap,
    SystemMemoryStream ShortlivedStream
)
{
    /// <summary>
    /// Holds the structure of a front-loaded write until its size is known; null otherwise, and once
    /// the structure is placed. See <see cref="StructureStaging" />.
    /// </summary>
    public StructureStaging? Staging { get; set; }
}

/// <summary>
/// What a front-loaded write that encodes its structure once has to finish once the structure's size is
/// known: the payload addresses the structure names, and the payload that could not be written yet.
/// </summary>
/// <remarks>
/// Payload is placed after the structure, so until the structure is complete a payload address is
/// provisional - see <see cref="FreeSpaceManager.ReserveProvisionalRegion" />. Structure naming one is
/// held in <see cref="Stream" /> and corrected before it is written, and payload given with the tree is
/// written once there is somewhere final to write it.
/// </remarks>
internal sealed class StructureStaging(StructureStagingStream stream)
{
    public StructureStagingStream Stream { get; } = stream;

    public List<ProvisionalAddress> Addresses { get; } = [];

    public List<Action> PostponedWrites { get; } = [];
}

/// <summary>
/// A provisional payload address encoded in an object header: where the header is, and where in it the
/// address is.
/// </summary>
internal readonly record struct ProvisionalAddress(long HeaderPosition, int HeaderLength, int AddressOffset);
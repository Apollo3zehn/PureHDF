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
/// What the writer knows about an object it has linked: where its header is, and for a dataset, what
/// it needs to write the dataset's data later.
/// </summary>
/// <remarks>
/// One entry per object, in one map, rather than a map of addresses and another of datasets keyed by
/// the same objects. A mutable struct so an entry can be completed in place: it is added when its
/// object starts encoding (<see cref="Address" /> still zero, which is how a circular reference is
/// detected) and gets its address when encoding finishes.
/// </remarks>
internal struct EncodedObject
{
    public ulong Address;

    /// <summary>Set for a dataset whose data can still be written, otherwise null.</summary>
    public DeferredDataset? Deferred;
}

/// <summary>
/// What the writer keeps per dataset so that its data can be written after its object header.
/// </summary>
/// <remarks>
/// A file of many small datasets is almost all <see cref="DeferredContiguous" />, which holds no more
/// than where the data goes and what it looks like. Holding the full layout and messages for those
/// would be most of what the writer keeps per dataset.
/// </remarks>
internal abstract class DeferredDataset(object encode)
{
    /// <summary>The element encoder, an <c>EncodeDelegate</c> of the dataset's element type.</summary>
    public object Encode { get; } = encode;

    /// <summary>The layout kept to write the dataset's data, otherwise null.</summary>
    public virtual H5D_Base? H5D => null;

    /// <summary>
    /// The payload still to be allocated and the header still to be written, until the first write.
    /// See <see cref="H5NativeWriter.AllocatePendingPayload" />.
    /// </summary>
    public PendingPayload? Pending { get; set; }

    /// <summary>
    /// Where the header was allocated, for one allocated late; zero until then, and for every other
    /// dataset. See <see cref="H5NativeWriter.AllocatePendingHeader" />.
    /// </summary>
    public ulong LateHeaderAddress { get; set; }

    /// <summary>Records where the payload was allocated.</summary>
    public virtual void SetPayloadAddress(ulong address)
        => ((DataLayoutMessage4)GetInfo().Layout).Properties.Address = address;

    public abstract DatasetInfo GetInfo();

    /// <summary>
    /// Moves a provisionally placed payload address to where it ended up. See
    /// <see cref="FreeSpaceManager.ReserveProvisionalRegion" />.
    /// </summary>
    public virtual void Relocate(FreeSpaceManager freeSpaceManager)
        => RelocateLayout(GetInfo(), freeSpaceManager);

    public static void RelocateLayout(DatasetInfo info, FreeSpaceManager freeSpaceManager)
    {
        if (info.Layout is DataLayoutMessage4 layout)
            layout.Properties.Address = freeSpaceManager.Relocate(layout.Properties.Address);
    }
}

/// <summary>
/// A chunked dataset given its data with the tree, whose layout carries state from encoding until the
/// dataset is finished (<see cref="H5NativeWriter.FinishIfComplete" />) or the writer is disposed: its
/// chunk cache, and the index it allocates then.
/// </summary>
internal sealed class DeferredLayout(H5D_Base h5d, object encode) : DeferredDataset(encode)
{
    public override H5D_Base H5D { get; } = h5d;

    public override DatasetInfo GetInfo() => H5D.Dataset;
}

/// <summary>
/// The minimum to write a contiguous dataset's data: its dimensions and datatype, and where its data
/// goes. Everything else is built again for each write.
/// </summary>
internal sealed class DeferredContiguous(
    ulong[] fileDims, DatatypeMessage type, ulong address, ulong size, object encode) : DeferredDataset(encode)
{
    public ulong[] FileDims { get; } = fileDims;

    public DatatypeMessage Type { get; } = type;

    public ulong Address { get; private set; } = address;

    public override void Relocate(FreeSpaceManager freeSpaceManager)
        => Address = freeSpaceManager.Relocate(Address);

    public override void SetPayloadAddress(ulong address)
        => Address = address;

    public override DatasetInfo GetInfo() => new(
        Space: DataspaceMessage.Create(FileDims),
        Type: Type,
        Layout: new DataLayoutMessage4(
            LayoutClass: LayoutClass.Contiguous,
            Properties: new ContiguousStoragePropertyDescription(Size: size) { Address = Address })
        {
            Version = 4
        },
        FillValue: H5NativeWriter.DefaultFillValueMessage,
        FilterPipeline: default,
        ExternalFileList: default);
}

/// <summary>
/// A chunked dataset declared without data, whose layout is only created when it is first written.
/// </summary>
/// <remarks>
/// A chunked layout holds the chunk geometry, the chunks being filled and the index, and holding one
/// per dataset from BeginWrite on would be most of what a writer of many chunked datasets holds before
/// any is written. Until then, this keeps the dataset's messages, which the layout is created from.
/// </remarks>
internal sealed class DeferredChunked(DatasetInfo info, object encode) : DeferredDataset(encode)
{
    private H5D_Base? _h5d;

    public override H5D_Base? H5D => _h5d;

    public override DatasetInfo GetInfo() => info;

    public H5D_Base GetOrCreateLayout(H5NativeWriter writer, H5DatasetCreation datasetCreation)
        => _h5d ??= writer.CreateLayout(info, datasetCreation);
}

/// <summary>
/// A deferred dataset's payload, allocated on its first write rather than when it is declared, and the
/// header that names it, encoded but held back until then.
/// </summary>
/// <remarks>
/// Allocated when declared, the payload of every dataset not written yet is a gap behind the ones that
/// are, unless they are written in the order they were declared - and a destination uploading the file in
/// parts holds a part for every such gap. Allocated when first written, it lands at the end of the file in
/// the order the data arrives.
/// </remarks>
/// <param name="HeaderAddress">
/// Where the header is allocated; negative for a header not allocated yet, see
/// <see cref="H5NativeWriter.AllocatePendingHeader" />.
/// </param>
/// <param name="Header">The encoded header, with an undefined payload address.</param>
/// <param name="AddressOffset">Where in the header the payload address goes.</param>
/// <param name="Size">How much payload to allocate.</param>
internal sealed record PendingPayload(long HeaderAddress, byte[] Header, int AddressOffset, long Size);

/// <summary>
/// A dataset whose data has all been written and whose structure is final: a filtered chunked dataset
/// once its last chunk is written. Nothing of how it was written is kept.
/// </summary>
internal sealed class FinishedDataset() : DeferredDataset(encode: null!)
{
    public static FinishedDataset Instance { get; } = new();

    public override DatasetInfo GetInfo()
        => throw new InvalidOperationException("All of this dataset's data has been written.");

    public override void Relocate(FreeSpaceManager freeSpaceManager)
    {
    }
}

/// <summary>Anything else (compact, null dataspace): rare enough to keep in full.</summary>
internal sealed class DeferredInfo(DatasetInfo info, object encode) : DeferredDataset(encode)
{
    public override DatasetInfo GetInfo() => info;
}

internal record NativeWriteContext(
    H5NativeWriter Writer,
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Object, EncodedObject> Objects,
    Dictionary<DatasetInfo, (long ObjectHeaderAddress, byte[] ObjectHeader, bool IsWritten)> DatasetInfoToObjectHeaderMap,
    Dictionary<DatatypeCacheKey, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
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
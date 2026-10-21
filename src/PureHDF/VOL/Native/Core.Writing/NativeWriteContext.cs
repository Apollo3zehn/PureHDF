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
/// object starts encoding and gets its address when encoding finishes.
/// </remarks>
internal struct EncodedObject
{
    /// <summary>
    /// The object's header address. Zero means one of two things: the object is still being encoded,
    /// which is how a circular reference is detected; or, when streaming, its header has no address yet -
    /// <see cref="Deferred" /> holds it back, or <see cref="Group" /> waits - see <see cref="IPendingHeader" />.
    /// </summary>
    public ulong Address;

    /// <summary>Set for a dataset whose data can still be written, otherwise null.</summary>
    public DeferredDataset? Deferred;

    /// <summary>Set for a group whose header waits for its children, otherwise null. See <see cref="PendingGroup" />.</summary>
    public PendingGroup? Group;
}

/// <summary>
/// A header whose address may not be known yet: a deferred dataset's, allocated once the dataset is written,
/// or a group's waiting for one. See <see cref="PendingGroup" />.
/// </summary>
internal interface IPendingHeader
{
    /// <summary>The header's address; zero until it has one.</summary>
    ulong Address { get; }

    /// <summary>The group waiting for the address, if any.</summary>
    LinkWaiter? Waiter { get; set; }

    /// <summary>Gives the header its address now, and returns it.</summary>
    ulong Resolve(H5NativeWriter writer);
}

/// <summary>
/// A deferred dataset's encoded header, held until it is complete - once the dataset's data is written - and
/// written then. See <see cref="H5NativeWriter.AllocateHeader" />.
/// </summary>
/// <remarks>
/// Held in memory rather than written and patched, since rewriting it would change bytes a destination may
/// already have shipped. A chunked dataset's layout patches its index into it, see <see cref="H5D_Chunk4" />;
/// a contiguous dataset's first write its payload address, see <see cref="PendingPayload" />.
/// </remarks>
/// <param name="header">The encoded header.</param>
/// <param name="encodedAt">The address the header was encoded as if at, which positions within it are relative to.</param>
internal sealed class KeptHeader(byte[] header, ulong encodedAt) : IPendingHeader
{
    public byte[] Header { get; } = header;

    public ulong EncodedAt { get; } = encodedAt;

    /// <inheritdoc />
    /// <remarks>Allocated when the header was encoded, or late: when the dataset is written or something names it.</remarks>
    public ulong Address { get; set; }

    /// <summary>Whether the header has been written, or queued to be.</summary>
    public bool IsWritten { get; set; }

    /// <summary>Whether the header belongs to a shared object, written at the end.</summary>
    public bool IsShared { get; init; }

    /// <inheritdoc />
    public LinkWaiter? Waiter { get; set; }

    /// <inheritdoc />
    public ulong Resolve(H5NativeWriter writer) => writer.AllocateHeader(this);
}

/// <summary>
/// Where a header waiting for its address is named: a link of a <see cref="PendingGroup" />.
/// </summary>
internal readonly record struct LinkWaiter(PendingGroup Group, int Slot);

/// <summary>
/// A group of an appended subtree whose header waits until every child it links has an address: a deferred
/// dataset whose header is allocated once it is written, or another such group.
/// </summary>
/// <remarks>
/// Written at Append, the group's header would name its deferred datasets' headers, which would then have
/// to be allocated at Append and stay a gap until their data is written - behind everything written in the
/// meantime, and a destination uploading in parts would hold a part for them. Waiting, the group is written
/// once its last child has an address, which is when that child is written, or when something has to name
/// the group: its open group closing, another link to it, an object reference.
/// </remarks>
internal sealed class PendingGroup(List<HeaderMessage> headerMessages, List<(int MessageIndex, string Name, IPendingHeader? Target)> slots) : IPendingHeader
{
    /// <summary>The header's messages, with a placeholder for every link still waiting.</summary>
    public List<HeaderMessage> HeaderMessages { get; } = headerMessages;

    /// <summary>The links: where their message goes, and what they wait for - null once it has its address.</summary>
    public List<(int MessageIndex, string Name, IPendingHeader? Target)> Slots { get; } = slots;

    public int Unresolved { get; set; } = slots.Count;

    /// <inheritdoc />
    /// <remarks>Set once the header is written.</remarks>
    public ulong Address { get; set; }

    /// <inheritdoc />
    public LinkWaiter? Waiter { get; set; }

    /// <inheritdoc />
    public ulong Resolve(H5NativeWriter writer) => writer.ResolvePendingGroup(this);
}

/// <summary>
///     An object written by an earlier append: its header's address, or the group still waiting to be written;
///     or a group appended to stay open, which is linked only from where it was appended.
/// </summary>
internal sealed class WrittenObject(ulong address, PendingGroup? group, bool isOpen = false)
{
    public static WrittenObject Open { get; } = new(address: default, group: null, isOpen: true);

    public ulong Address { get; } = address;

    public PendingGroup? Group { get; } = group;

    public bool IsOpen { get; } = isOpen;
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
    /// The payload still to be allocated, until the first write. See
    /// <see cref="H5NativeWriter.AllocatePendingPayload" />.
    /// </summary>
    public PendingPayload? Pending { get; set; }

    /// <summary>The header held until it is complete, if it is; otherwise null, and it was written when encoded.</summary>
    public KeptHeader? Header { get; set; }

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
/// A deferred dataset's payload, allocated on its first write rather than when it is declared. The header
/// that names it is held until then, see <see cref="KeptHeader" />.
/// </summary>
/// <remarks>
/// Allocated when declared, the payload of every dataset not written yet is a gap behind the ones that
/// are, unless they are written in the order they were declared - and a destination uploading the file in
/// parts holds a part for every such gap. Allocated when first written, it lands at the end of the file in
/// the order the data arrives.
/// </remarks>
/// <param name="AddressOffset">Where in the dataset's <see cref="DeferredDataset.Header" /> the payload address goes.</param>
/// <param name="Size">How much payload to allocate.</param>
/// <param name="MayStayUnallocated">
/// Whether a dataset never written may be left without payload: a single chunk not allocated reads as fill
/// values, while contiguous data has to be there.
/// </param>
internal sealed record PendingPayload(int AddressOffset, long Size, bool MayStayUnallocated);

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
    Dictionary<DatasetInfo, KeptHeader> DatasetInfoToObjectHeaderMap,
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
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace PureHDF;

// Writing a file one appended subtree at a time - see H5StreamingWriter. Everything here is used only
// while streaming, except where it says otherwise.
partial class H5NativeWriter
{
    private StreamingState? _streaming;

    private bool IsStreaming => _streaming is not null;

    private StreamingState Streaming => _streaming ?? throw new InvalidOperationException("The writer is not streaming.");

    // Appending

    /// <summary>
    /// Prepares a writer that is given its tree one subtree at a time. See <see cref="H5StreamingWriter" />.
    /// </summary>
    internal void BeginStreaming()
    {
        _streaming = new StreamingState();
        Context.FreeSpaceManager.ReserveSharedRegion(Context.WriteOptions.SharedObjectRegionSize);
        Context.Driver.SeekRelativeToBaseAddress(Superblock23.ENCODE_SIZE);
    }

    /// <summary>
    /// Encodes an appended subtree and returns its address, or zero for one whose header waits - see
    /// <see cref="GetPendingHeader" />.
    /// </summary>
    /// <remarks>
    /// A deferred dataset is named by nothing written yet: only its group's header will name it. So its
    /// header is not allocated until it is first written or something has to name it, and in the meantime
    /// it is no gap in the file. Appended directly, that is its open group closing; inside an appended
    /// group, the group waits for it - see <see cref="PendingGroup" />.
    /// </remarks>
    internal ulong EncodeAppended(H5Object subtree)
    {
        Streaming.LinksCountedByAppend.Clear();
        Streaming.SharedHeadersBeforeAppend = Streaming.SharedHeaders.Count;

        if (subtree is H5Group group)
            CountReferences(group, Context.ObjectReferenceCountMap);

        CountLink(subtree);

        return GetOrEncodeObject(subtree, allowsLateHeader: true);
    }

    /// <summary>
    /// Finishes an append: records what it wrote, and drops everything kept about it that writing its
    /// deferred datasets does not need.
    /// </summary>
    internal void EndAppend()
    {
        var written = Streaming.Written;

        foreach (var h5Object in Streaming.Appended)
        {
            var entry = Context.Objects[h5Object];

            written.AddOrUpdate(h5Object, new WrittenObject(entry.Address, entry.Group));

            // A dataset given with data is written, and its entry would keep the data alive: only those
            // declared without data are kept, to be written through the writer.
            if (entry.Deferred is null or FinishedDataset)
            {
                Context.Objects.Remove(h5Object);
            }

            else if (h5Object is H5Dataset { Data: not null })
            {
                if (entry.Deferred.H5D is { } h5d)
                    Streaming.DetachedLayouts.Add(h5d);

                Context.Objects.Remove(h5Object);
            }
        }

        Streaming.Appended.Clear();
        Context.ObjectReferenceCountMap.Clear();
        Context.RawValueToDatasetMap.Clear();

        EndTopLevelEncode();
    }

    /// <summary>
    /// Undoes what a failed append changed in what the writer keeps, so that it can go on: forgets the
    /// objects it encoded and the links it counted.
    /// </summary>
    /// <remarks>
    /// What it wrote stays in the file, named by nothing.
    /// </remarks>
    internal void AbortAppend()
    {
        foreach (var h5Object in Streaming.Appended)
            Context.Objects.Remove(h5Object);

        foreach (var counter in Streaming.LinksCountedByAppend)
            counter.Links--;

        Streaming.SharedHeaders.RemoveRange(Streaming.SharedHeadersBeforeAppend, Streaming.SharedHeaders.Count - Streaming.SharedHeadersBeforeAppend);

        Streaming.Appended.Clear();
        Streaming.LinksCountedByAppend.Clear();
        Context.ObjectReferenceCountMap.Clear();
        Context.RawValueToDatasetMap.Clear();

        EndTopLevelEncode();
    }

    /// <summary>
    /// Whether <paramref name="h5Object" /> was written by an earlier append.
    /// </summary>
    internal bool IsWritten(H5Object h5Object)
        => _streaming is not null && _streaming.Written.TryGetValue(h5Object, out _);

    /// <summary>
    /// Whether all of <paramref name="dataset" />'s data has been written by an earlier append: it was given
    /// with data, or it is a filtered dataset whose chunks are all written. Neither is kept, see
    /// <see cref="EndAppend" />.
    /// </summary>
    internal bool IsFinished(H5Dataset dataset)
        => !Context.Objects.ContainsKey(dataset) && IsWritten(dataset);

    // Headers waiting for an address - see IPendingHeader

    /// <summary>
    /// The header of <paramref name="h5Object" /> if it has no address yet: a deferred dataset's allocated
    /// late, or a group's waiting for one. Null once it has an address.
    /// </summary>
    internal IPendingHeader? GetPendingHeader(H5Object h5Object)
        => Context.Objects.GetValueOrDefault(h5Object) switch
        {
            { Address: 0, Deferred.Header: { Address: 0 } header } => header,
            { Address: 0, Group: { Address: 0 } group } => group,
            _ => null
        };

    /// <summary>
    /// Writes a waiting group now: allocates what its children wait for, and its header.
    /// </summary>
    internal ulong ResolvePendingGroup(PendingGroup group)
    {
        // Each child given an address resolves its slot, and the last one writes the group.
        for (var slot = 0; slot < group.Slots.Count && group.Address == 0; slot++)
            group.Slots[slot].Target?.Resolve(this);

        return group.Address;
    }

    /// <summary>
    /// Fills in a waiting group's link to a child that now has an address, and writes the group once it was
    /// the last.
    /// </summary>
    private void ResolveSlot(LinkWaiter waiter, ulong address)
    {
        var group = waiter.Group;
        var (messageIndex, name, _) = group.Slots[waiter.Slot];

        group.HeaderMessages[messageIndex] = CreateLinkMessage(name, LinkType.Hard, new HardLinkInfo(HeaderAddress: address));
        group.Slots[waiter.Slot] = (messageIndex, name, null);

        if (--group.Unresolved > 0)
            return;

        group.Address = EncodeObjectHeader(group.HeaderMessages, owner: null);
        group.HeaderMessages.Clear();

        if (group.Waiter is { } next)
        {
            group.Waiter = null;
            ResolveSlot(next, group.Address);
        }
    }

    // Shared objects - see H5Object.IsShared

    /// <summary>
    /// Whether <paramref name="h5Object" />'s header is held until the end and written into the shared
    /// region. See <see cref="H5Object.IsShared" />.
    /// </summary>
    private bool IsHeldShared(H5Object h5Object)
        => IsStreaming && h5Object.IsShared && h5Object.HardLinkCount is null;

    /// <summary>
    /// Encodes the header of a shared object into the shared region, and keeps it to be written at the
    /// end with its final link count.
    /// </summary>
    private KeptHeader KeepSharedHeader(ObjectHeader2 objectHeader, H5Object owner)
    {
        var address = Context.FreeSpaceManager.AllocateShared(objectHeader.GetEncodeSize());
        var encoded = objectHeader.EncodeAt(Context, (ulong)address);

        var countMessage = objectHeader.HeaderMessages
            .Select(message => message.Data)
            .OfType<ObjectReferenceCountMessage>()
            .Single();

        var countOffset = countMessage.CountPosition - ((long)Context.Driver.BaseAddress + address);

        Streaming.SharedHeaders.Add(new SharedHeader(address, encoded, (int)countOffset, GetLinkCounter(owner)));

        return new KeptHeader(encoded, encodedAt: (ulong)address) { Address = (ulong)address, IsWritten = true, IsShared = true };
    }

    /// <summary>
    /// Writes the shared regions: the headers of the shared objects with their final link counts, and
    /// zeros between and after them.
    /// </summary>
    internal void WriteSharedRegions()
    {
        var regions = Context.FreeSpaceManager.SharedRegions;

        if (regions.Count == 0)
            return;

        var driver = Context.Driver;
        var position = driver.Position;
        var headers = 0;

        Streaming.SharedHeaders.Sort((x, y) => x.Address.CompareTo(y.Address));

        foreach (var (regionAddress, regionLength) in regions)
        {
            var buffer = new byte[regionLength];

            for (; headers < Streaming.SharedHeaders.Count && Streaming.SharedHeaders[headers].Address < regionAddress + regionLength; headers++)
            {
                var (address, header, countOffset, counter) = Streaming.SharedHeaders[headers];

                // An object only referenced, never linked, counts as linked once - as it does when the
                // count message is left out.
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(countOffset), (uint)Math.Max(1, counter.Links));
                ChecksummedStructure.UpdateChecksum(header);

                header.CopyTo(buffer.AsSpan((int)(address - regionAddress)));
            }

            driver.SeekRelativeToBaseAddress(regionAddress);
            driver.Write(buffer);
        }

        driver.Seek(position, SeekOrigin.Begin);
        Streaming.SharedHeaders.Clear();
    }

    // Finishing

    /// <summary>
    /// Finishes the file once the root group, the last open group, is closed.
    /// </summary>
    internal void FinishStreaming(ulong rootGroupAddress)
    {
        _rootGroupAddress = rootGroupAddress;
        Dispose();
    }

    /// <summary>
    /// What a streaming writer keeps beyond what any writer does.
    /// </summary>
    private sealed class StreamingState
    {
        /// <summary>
        /// The objects written by earlier appends, and their header addresses. Weak, so it keeps nothing
        /// alive the caller has let go of. See <see cref="EndAppend" />.
        /// </summary>
        public ConditionalWeakTable<H5Object, WrittenObject> Written { get; } = new();

        /// <summary>The headers of shared objects, written at the end. See <see cref="H5Object.IsShared" />.</summary>
        public List<SharedHeader> SharedHeaders { get; } = [];

        /// <summary>
        /// Layouts of datasets given with data whose map entries <see cref="EndAppend" /> dropped, still to be
        /// disposed - the index of a chunked dataset is written then.
        /// </summary>
        public List<H5D_Base> DetachedLayouts { get; } = [];

        /// <summary>
        /// The objects encoded by the current append. The map holds the deferred datasets of every earlier
        /// append too, so walking it at every append would grow with the file.
        /// </summary>
        public List<H5Object> Appended { get; } = [];

        /// <summary>The links the current append counted, which <see cref="AbortAppend" /> undoes.</summary>
        public List<LinkCounter> LinksCountedByAppend { get; } = [];

        /// <summary>How many shared headers there were before the current append, for <see cref="AbortAppend" />.</summary>
        public int SharedHeadersBeforeAppend { get; set; }
    }

    private readonly record struct SharedHeader(long Address, byte[] Header, int CountOffset, LinkCounter Counter);
}

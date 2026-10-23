using PureHDF.Selections;

namespace PureHDF;

/// <summary>
/// A writer that writes a file piece by piece: each subtree is written when it is appended, and the
/// writer holds on to no more of it than its deferred datasets need.
/// </summary>
/// <remarks>
/// <para>
/// Groups on an appended path, and groups created by <see cref="Group(string)" />, are open until they are closed
/// - by <see cref="Close" /> or at <see cref="Dispose" /> - and hold one entry per link until then. An
/// appended subtree is final: changing it afterwards does not change the file.
/// </para>
/// <para>
/// Everything is written once, in the order it is allocated, except the superblock at the start of the
/// file and the headers of shared objects (both written at <see cref="Dispose" />), and the headers of
/// deferred datasets (written once their data is, or at Dispose). Space allocated and never written - the
/// unused tail of a metadata block, the payload of a deferred dataset never written - reads as zeros on
/// a file system; a destination that is not a file has to fill it with zeros itself.
/// </para>
/// </remarks>
public sealed class H5StreamingWriter : IDisposable
{
    private readonly OpenGroup _root;
    private bool _disposed;

    internal H5StreamingWriter(Stream stream, H5WriteOptions options, bool leaveOpen)
    {
        NativeWriter = new H5NativeWriter(new H5File(), stream, options, leaveOpen);
        NativeWriter.BeginStreaming();
        _root = new OpenGroup(handle: null);
    }

    internal H5NativeWriter NativeWriter { get; }

    /// <summary>
    /// The root group. Its attributes can be set until the writer is disposed, which is when the root
    /// group's header is written.
    /// </summary>
    public H5Object Root => NativeWriter.File;

    /// <summary>
    /// Writes <paramref name="subtree" /> and links it at <paramref name="path" />.
    /// </summary>
    /// <remarks>
    /// Every group on the path that does not exist yet is created and stays open; its attributes are set
    /// through <see cref="Group(string)" />. The last segment of the path is the name of the new link. Datasets
    /// declared without data are written later with <see cref="Write{T}" />.
    /// <para>
    /// An append that throws links nothing, and the writer can go on. What it had written already stays in
    /// the file, named by nothing.
    /// </para>
    /// </remarks>
    /// <param name="path">The path of the new link, relative to the root group, its segments separated by '/'.</param>
    /// <param name="subtree">The group or dataset to write.</param>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    /// The path leads through a closed group or a dataset, or its last segment is a link that exists
    /// already; or <paramref name="subtree" /> has already been written and does not declare a
    /// <see cref="H5Object.HardLinkCount" /> that allows another link.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void Append(string path, H5Object subtree)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(subtree);

        var (parent, existing, segments) = FindNewLink(path);
        var name = segments[^1];

        ulong address;

        try
        {
            address = NativeWriter.EncodeAppended(subtree);
        }

        catch
        {
            NativeWriter.AbortAppend();
            throw;
        }

        parent = CreateGroups(parent, segments, existing, segments.Length - 1);

        parent.Links.Add(name, NativeWriter.GetPendingHeader(subtree) is { } pending
            ? new OpenLink(Address: default, Pending: pending)
            : new OpenLink(address, Pending: null));

        NativeWriter.EndAppend();
    }

    /// <summary>
    ///     Writes the children of <paramref name="group" />, links the group at <paramref name="path" />, and
    ///     keeps it open: it takes further links, and its attributes can be set, until it is closed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The children are written as an appended subtree is, in one pass: a child linked more than once below
    ///         the group needs no <see cref="H5Object.HardLinkCount" />. The group's attributes are copied into the
    ///         open group's, and its header is written when the open group closes. Changing
    ///         <paramref name="group" /> afterwards does not change the file; the open group returned does.
    ///     </para>
    ///     <para>
    ///         An open group is linked only from its parent: <paramref name="group" /> cannot be linked or referenced
    ///         again, from below itself or by a later append.
    ///     </para>
    ///     <para>
    ///         Every group on the path that does not exist yet is created and stays open. An append that throws
    ///         links nothing, and the writer can go on. What it had written already stays in the file, named by
    ///         nothing.
    ///     </para>
    /// </remarks>
    /// <param name="path">The path of the new link, relative to the root group, its segments separated by '/'.</param>
    /// <param name="group">The group to write and keep open.</param>
    /// <returns>The open group.</returns>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The path leads through a closed group or a dataset, or its last segment is a link that exists
    ///     already; or <paramref name="group" /> declares a <see cref="H5Object.HardLinkCount" /> above one, is
    ///     <see cref="H5Object.IsShared" />, has already been appended, or is linked or referenced from below
    ///     itself.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public H5OpenGroup AppendOpen(string path, H5Group group)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(group);

        if (group.HardLinkCount > 1 || group.IsShared)
            throw new InvalidOperationException(
                "An open group is linked only from its parent: it cannot declare more than one hard link or be shared.");

        var (parent, existing, segments) = FindNewLink(path);

        List<(string Name, ulong Address, object? Pending)> links;

        try
        {
            links = NativeWriter.EncodeAppendedOpen(group);
        }

        catch
        {
            NativeWriter.AbortAppend();
            throw;
        }

        parent = CreateGroups(parent, segments, existing, segments.Length);

        foreach (var (name, address, pending) in links)
            parent.Links.Add(name, new OpenLink(address, pending));

        var handle = parent.Handle!;

        if (group.InternalAttributes is { } attributes)
        {
            foreach (var (name, value) in attributes)
                handle.Attributes[name] = value;
        }

        NativeWriter.EndAppend();

        return handle;
    }

    /// <summary>
    ///     The open group at <paramref name="path" />, created - along with every group on the path that does
    ///     not exist yet - if it does not exist. Its attributes can be set until it is closed, which is when its
    ///     header is written.
    /// </summary>
    /// <param name="path">The path of the group, relative to the root group, its segments separated by '/'.</param>
    /// <returns>The open group, the same one for each call with this path.</returns>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The path leads through a closed group or a dataset, or names one.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public H5OpenGroup Group(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var segments = SplitPath(path);
        var (parent, existing) = FindParentGroups(segments);
        var name = segments[^1];

        if (existing == segments.Length - 1 && parent.Links.TryGetValue(name, out var link))
        {
            return link.Pending is OpenGroup { Handle: { } handle }
                ? handle
                : throw new InvalidOperationException(
                    $"'{path}' is not an open group: it is a dataset, a soft link, or a group that has been closed.");
        }

        return CreateGroups(parent, segments, existing, segments.Length).Handle!;
    }

    /// <summary>
    ///     The open group at <paramref name="path" />, as <see cref="Group(string)" /> returns it, with
    ///     <paramref name="attributes" /> added to its attributes.
    /// </summary>
    /// <remarks>
    ///     The attributes are copied: changing <paramref name="attributes" /> afterwards does not change the
    ///     group. An attribute the group has already is overwritten. Groups created on the way to the group get
    ///     none of them.
    /// </remarks>
    /// <param name="path">The path of the group, relative to the root group, its segments separated by '/'.</param>
    /// <param name="attributes">The attributes to add.</param>
    /// <returns>The open group, the same one for each call with this path.</returns>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The path leads through a closed group or a dataset, or names one.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public H5OpenGroup Group(string path, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var group = Group(path);

        foreach (var (name, value) in attributes)
            group.Attributes[name] = value;

        return group;
    }

    /// <summary>
    /// Writes data to a dataset of an appended subtree that was declared without data.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="dataset">The dataset to write data to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="memorySelection">The memory selection.</param>
    /// <param name="fileSelection">The file selection.</param>
    /// <exception cref="InvalidOperationException">
    /// All of the dataset's data has been written: it was given with data, or it is a filtered dataset whose
    /// chunks have all been written.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void Write<T>(
        H5Dataset<T> dataset,
        T data,
        Selection? memorySelection = default,
        Selection? fileSelection = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (NativeWriter.IsFinished(dataset))
            throw new InvalidOperationException(
                "All of this dataset's data has been written: it was given with data, and written when it was " +
                "appended, or it is a filtered dataset whose chunks have all been written. Only datasets declared " +
                "without data are written through Write.");

        NativeWriter.Write(dataset, data, memorySelection, fileSelection);
    }

    /// <summary>
    /// Closes the open group at <paramref name="path" /> and every open group below it, and writes their
    /// headers. Nothing can be appended below a closed group.
    /// </summary>
    /// <param name="path">The path of the group, relative to the root group.</param>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">The path is not an open group.</exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void Close(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var segments = SplitPath(path);
        var (parent, existing) = FindParentGroups(segments);
        var name = segments[^1];

        if (existing < segments.Length - 1 ||
            !parent.Links.TryGetValue(name, out var link) ||
            link.Pending is not OpenGroup group)
        {
            throw new InvalidOperationException($"'{path}' is not an open group.");
        }

        parent.Links[name] = new OpenLink(CloseGroup(group), Pending: null);
        NativeWriter.EndTopLevelEncode();
    }

    /// <summary>
    ///     Finds where a new link at <paramref name="path" /> goes. The groups on the path that do not exist yet
    ///     are not created, so that an append that fails leaves none behind.
    /// </summary>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The path leads through a closed group or a dataset, or its last segment is a link that exists already.
    /// </exception>
    private (OpenGroup Parent, int Depth, string[] Segments) FindNewLink(string path)
    {
        var segments = SplitPath(path);
        var (parent, existing) = FindParentGroups(segments);

        if (existing == segments.Length - 1 && parent.Links.ContainsKey(segments[^1]))
            throw new InvalidOperationException($"The link '{path}' exists already.");

        return (parent, existing, segments);
    }

    /// <summary>
    /// Follows a path's parent segments - all but the last - through the open groups, as far as they exist.
    /// </summary>
    /// <returns>The last open group reached, and how many segments lead to it.</returns>
    /// <exception cref="InvalidOperationException">A segment names a dataset or a closed group.</exception>
    private (OpenGroup Group, int Depth) FindParentGroups(string[] segments)
    {
        var group = _root;
        var depth = 0;

        for (; depth < segments.Length - 1; depth++)
        {
            if (!group.Links.TryGetValue(segments[depth], out var link))
                break;

            group = link.Pending as OpenGroup ?? throw new InvalidOperationException(
                $"'{string.Join('/', segments[..(depth + 1)])}' is not an open group: it is a dataset, a soft link, or a group that has been closed.");
        }

        return (group, depth);
    }

    /// <summary>
    ///     Creates a chain of open groups below <paramref name="parent" />, one per segment from
    ///     <paramref name="start" /> up to, not including, <paramref name="end" />.
    /// </summary>
    /// <returns>The last group created, or <paramref name="parent" /> when there are no segments.</returns>
    private OpenGroup CreateGroups(OpenGroup parent, string[] segments, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            var group = new OpenGroup(new H5OpenGroup(this, string.Join('/', segments[..(i + 1)])));
            parent.Links.Add(segments[i], new OpenLink(Address: default, Pending: group));
            parent = group;
        }

        return parent;
    }

    /// <summary>
    /// Closes all open groups, finishes the deferred datasets, and writes the rest of the file.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An object is linked fewer times than its <see cref="H5Object.HardLinkCount" /> declares. The file is
    /// complete, and that object's header claims more links than it has.
    /// </exception>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        NativeWriter.FinishStreaming(CloseGroup(_root));

        // Checked once the file is complete: a file with a count too high reads fine, while one without its
        // superblock does not read at all.
        NativeWriter.ValidateDeclaredLinks();
    }

    // Bottom-up: a group's links name its children's headers, so those are written first.
    private ulong CloseGroup(OpenGroup group)
    {
        var links = new List<(string Name, ulong Address, H5SoftLink? SoftLink)>(group.Links.Count);

        foreach (var (name, link) in group.Links)
        {
            if (link.Pending is H5SoftLink softLink)
            {
                links.Add((name, Address: default, softLink));
                continue;
            }

            var address = link.Pending switch
            {
                OpenGroup child => CloseGroup(child),
                IPendingHeader pending => pending.Resolve(NativeWriter),
                _ => link.Address
            };

            links.Add((name, address, SoftLink: null));
        }

        var attributes = group.Handle is null
            ? NativeWriter.File.InternalAttributes
            : group.Handle.MarkClosed();

        return NativeWriter.EncodeGroup(attributes, links);
    }

    private static string[] SplitPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var segments = path.Split('/');

        if (segments.Any(string.IsNullOrEmpty))
            throw new ArgumentException($"The path '{path}' is empty or has an empty segment.", nameof(path));

        return segments;
    }

    internal static H5WriteOptions ValidateOptions(H5WriteOptions? options)
    {
        options ??= new H5WriteOptions();

        if (options.SharedObjectRegionSize <= 0)
            throw new ArgumentException("The shared object region size must be greater than zero.", nameof(options));

        if (options.MetadataReservation != 0)
            throw new ArgumentException(
                "Streaming clusters structure into blocks as it goes; a metadata reservation is not supported.", nameof(options));

        // Streaming exists for destinations that cannot take a gap back, so payload is always allocated in
        // the order it is written.
        return options with
        {
            MetadataPlacement = H5MetadataPlacement.Aggregated,
            AllocatePayloadOnFirstWrite = true
        };
    }

    /// <summary>A group still taking links.</summary>
    /// <param name="handle">The caller's view of the group, which carries its attributes; null for the root group.</param>
    private sealed class OpenGroup(H5OpenGroup? handle)
    {
        public H5OpenGroup? Handle { get; } = handle;

        public Dictionary<string, OpenLink> Links { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// A link in an open group: the address of what it points to, or what is still to be written for
    /// it - an open group, a header without an address yet (<see cref="IPendingHeader" />), or a soft link.
    /// </summary>
    private readonly record struct OpenLink(ulong Address, object? Pending);
}

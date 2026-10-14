using PureHDF.Selections;

namespace PureHDF;

/// <summary>
/// A writer that writes a file piece by piece: each subtree is written when it is appended, and the
/// writer holds on to no more of it than its deferred datasets need.
/// </summary>
/// <remarks>
/// <para>
/// Groups on an appended path are open until they are closed - by <see cref="Close" /> or at
/// <see cref="Dispose" /> - and hold one entry per link until then. An appended subtree is final: changing
/// it afterwards does not change the file.
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
    private readonly OpenGroup _root = new();
    private bool _disposed;

    internal H5StreamingWriter(Stream stream, H5WriteOptions options, bool leaveOpen)
    {
        NativeWriter = new H5NativeWriter(new H5File(), stream, options, leaveOpen);
        NativeWriter.BeginStreaming();
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
    /// Every group on the path that does not exist yet is created, without attributes, and stays open.
    /// The last segment of the path is the name of the new link. Datasets declared without data are
    /// written later with <see cref="Write{T}" />.
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
    /// <remarks>
    /// An append that throws links nothing, and the writer can go on. What it had written already stays in
    /// the file, named by nothing.
    /// </remarks>
    public void Append(string path, H5Object subtree)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(subtree);

        var segments = SplitPath(path);

        // The groups on the path that do not exist yet are created only once the subtree is written, so
        // that a failed append leaves none behind.
        var (parent, existing) = FindOpenGroups(segments, segments.Length - 1);
        var name = segments[^1];

        if (existing == segments.Length - 1 && parent.Links.ContainsKey(name))
            throw new InvalidOperationException($"The link '{path}' exists already.");

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

        for (var i = existing; i < segments.Length - 1; i++)
        {
            var group = new OpenGroup();
            parent.Links.Add(segments[i], new OpenLink(Address: default, Pending: group));
            parent = group;
        }

        parent.Links.Add(name, NativeWriter.GetPendingHeader(subtree) is { } pending
            ? new OpenLink(Address: default, Pending: pending)
            : new OpenLink(address, Pending: null));

        NativeWriter.EndAppend();
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

        // Not kept once written, see H5NativeWriter.EndAppend.
        if (!NativeWriter.Context.Objects.ContainsKey(dataset) && NativeWriter.IsWritten(dataset))
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
        var (parent, existing) = FindOpenGroups(segments, segments.Length - 1);
        var name = segments[^1];

        if (existing < segments.Length - 1 ||
            !parent.Links.TryGetValue(name, out var link) ||
            link.Pending is not OpenGroup group)
        {
            throw new InvalidOperationException($"'{path}' is not an open group.");
        }

        parent.Links[name] = new OpenLink(CloseGroup(group, attributes: null), Pending: null);
        NativeWriter.EndTopLevelEncode();
    }

    /// <summary>
    /// Follows the first <paramref name="count" /> segments of a path through the open groups, as far as
    /// they exist.
    /// </summary>
    /// <returns>The last open group reached, and how many segments lead to it.</returns>
    /// <exception cref="InvalidOperationException">A segment names a dataset or a closed group.</exception>
    private (OpenGroup Group, int Depth) FindOpenGroups(string[] segments, int count)
    {
        var group = _root;

        for (var depth = 0; depth < count; depth++)
        {
            if (!group.Links.TryGetValue(segments[depth], out var link))
                return (group, depth);

            group = link.Pending as OpenGroup ?? throw new InvalidOperationException(
                $"'{string.Join('/', segments[..(depth + 1)])}' is not an open group: it is a dataset, or a group that has been closed.");
        }

        return (group, count);
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

        NativeWriter.FinishStreaming(CloseGroup(_root, NativeWriter.File.InternalAttributes));

        // Checked once the file is complete: a file with a count too high reads fine, while one without its
        // superblock does not read at all.
        NativeWriter.ValidateDeclaredLinks();
    }

    // Bottom-up: a group's links name its children's headers, so those are written first.
    private ulong CloseGroup(OpenGroup group, Dictionary<string, object>? attributes)
    {
        var links = new List<KeyValuePair<string, ulong>>(group.Links.Count);

        foreach (var (name, link) in group.Links)
        {
            var address = link.Pending switch
            {
                OpenGroup child => CloseGroup(child, attributes: null),
                IPendingHeader pending => pending.Resolve(NativeWriter),
                _ => link.Address
            };

            links.Add(new(name, address));
        }

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
    private sealed class OpenGroup
    {
        public Dictionary<string, OpenLink> Links { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// A link in an open group: the address of what it points to, or what is still to be written for
    /// it - an open group, or a header without an address yet (<see cref="IPendingHeader" />).
    /// </summary>
    private readonly record struct OpenLink(ulong Address, object? Pending);
}

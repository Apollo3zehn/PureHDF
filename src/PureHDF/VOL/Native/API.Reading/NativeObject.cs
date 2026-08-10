using System.Runtime.CompilerServices;
using System.Text;

namespace PureHDF.VOL.Native;

/// <inheritdoc />
public abstract class NativeObject : IH5Object
{
    #region Fields

    private ObjectHeader? _header;
    private ObjectReferenceCountMessage? _objectReferenceCount;

    #endregion

    #region Constructors

    internal NativeObject(NativeReadContext context, NativeNamedReference reference)
    {
        Context = context;
        Reference = reference;
    }

    internal NativeObject(NativeReadContext context, NativeNamedReference reference, ObjectHeader header)
    {
        Context = context;
        Reference = reference;
        _header = header;
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public string Name => Reference.Name;

    internal NativeReadContext Context { get; }

    internal uint ReferenceCount => GetReferenceCount();

    internal NativeNamedReference Reference { get; set; }

    private ObjectReferenceCountMessage? ObjectReferenceCount
    {
        get
        {
            _objectReferenceCount ??= Header
                    .GetMessages<ObjectReferenceCountMessage>()
                    .FirstOrDefault();

            return _objectReferenceCount;
        }
    }

    // CONCURRENCY: materializing the header is a driver read, so it takes a scope of its own rather
    // than moving the shared file-level cursor. Nesting inside an enclosing navigation scope is fine
    // - the outer operation holds the reuse slot, so this one allocates its own driver and hands it
    // back on dispose - and it happens at most once per object, because the result is cached.
    //
    // Two threads racing here both decode a valid header through their own driver and one instance
    // wins; the loser's copy is equivalent and still usable by whoever holds it. Volatile is used so
    // a reader can never observe a published-but-not-yet-initialized ObjectHeader. Before the scope
    // existed this race corrupted the cursor instead of merely duplicating work.
    //
    // Blocks on the async materialization when the header is not cached yet. An async caller must
    // therefore await GetHeader() FIRST and only then touch this property: after that it is a cached
    // field read that cannot block, which is what lets GetMessages<T>() stay synchronous all the way
    // down instead of every message query becoming a ValueTask.
    private protected ObjectHeader Header
    {
        get
        {
            var header = Volatile.Read(ref _header);

            return header ?? MaterializeHeader().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Returns this object's header, decoding it on first use.
    /// </summary>
    /// <remarks>
    /// The async counterpart of <see cref="Header" />, and the reason the property above can stay
    /// synchronous: an async operation awaits this once at its start, which populates the cache, and
    /// every later <c>Header</c> read inside that operation is then free.
    /// <para>
    /// Not <c>async</c> itself, because the cached case is by far the common one - an object is
    /// navigated more often than it is constructed - and it must not pay for a state machine.
    /// </para>
    /// </remarks>
    private protected ValueTask<ObjectHeader> GetHeader()
    {
        var header = Volatile.Read(ref _header);

        return header is null
            ? MaterializeHeader()
            : new ValueTask<ObjectHeader>(header);
    }

    private async ValueTask<ObjectHeader> MaterializeHeader()
    {
        using var scope = new NativeOperationScope(Context);

        scope.Context.Driver.SeekRelativeToBaseAddress((long)Reference.Value);

        var header = await ObjectHeader.Construct(scope.Context).ConfigureAwait(false);

        Volatile.Write(ref _header, header);

        return header;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public IEnumerable<IH5Attribute> Attributes()
    {
        return EnumerateAttributes();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<IH5Attribute>> AttributesAsync(CancellationToken cancellationToken = default)
    {
        // Materializes rather than streaming, because IH5Object promises Task<IEnumerable<...>> and
        // not IAsyncEnumerable<...>. The synchronous Attributes() below stays lazy.
        using var scope = new NativeOperationScope(Context);

        var attributes = new List<IH5Attribute>();

        await foreach (var attributeMessage in EnumerateAttributeMessages(scope.Context, cancellationToken))
        {
            attributes.Add(new NativeAttribute(Context, attributeMessage));
        }

        return attributes;
    }

    /// <inheritdoc />
    public IH5Attribute Attribute(string name)
    {
        // CONCURRENCY: one scope for the whole lookup - see the note on Header above. The returned
        // NativeAttribute outlives this operation and so keeps the FILE-LEVEL context; it opens a
        // scope of its own on every Read.
        using var scope = new NativeOperationScope(Context);

        // NOTE (async propagation): Attribute() is part of the synchronous public API
        // surface and must block on the async internals by design.
        var (success, attributeMessage) = TryGetAttributeMessage(scope.Context, name).GetAwaiter().GetResult();

        if (!success || attributeMessage is null)
            throw new Exception($"Could not find attribute '{name}'.");

        return new NativeAttribute(Context, attributeMessage);
    }

    /// <inheritdoc />
    public async Task<IH5Attribute> AttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        using var scope = new NativeOperationScope(Context);

        var (success, attributeMessage) = await TryGetAttributeMessage(scope.Context, name).ConfigureAwait(false);

        if (!success || attributeMessage is null)
            throw new Exception($"Could not find attribute '{name}'.");

        return new NativeAttribute(Context, attributeMessage);
    }

    /// <inheritdoc />
    public bool AttributeExists(string name)
    {
        // CONCURRENCY: one scope for the whole lookup - see the note on Header above.
        using var scope = new NativeOperationScope(Context);

        // NOTE (async propagation): AttributeExists() is part of the synchronous
        // public API surface and must block on the async internals by design.
        var (success, _) = TryGetAttributeMessage(scope.Context, name).GetAwaiter().GetResult();
        return success;
    }

    /// <inheritdoc />
    public async Task<bool> AttributeExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        using var scope = new NativeOperationScope(Context);

        var (success, _) = await TryGetAttributeMessage(scope.Context, name).ConfigureAwait(false);

        return success;
    }

    // Serves BOTH public surfaces: Attribute()/AttributeExists() bridge over it, while
    // AttributeAsync()/AttributeExistsAsync() await it. An `out` parameter cannot coexist with
    // `async` (CS1988), hence the tuple return.
    private async ValueTask<(bool Success, AttributeMessage? AttributeMessage)> TryGetAttributeMessage(
        NativeReadContext context,
        string name)
    {
        // Awaited once here so that every `Header` read below is a cached field read - see GetHeader.
        var header = await GetHeader().ConfigureAwait(false);

        // get attribute from attribute message
        var attributeMessage = header
            .GetMessages<AttributeMessage>()
            .FirstOrDefault(message => message.Name == name);

        if (attributeMessage is not null)
        {
            return (true, attributeMessage);
        }

        // get attribute from attribute info
        else
        {
            var attributeInfoMessages = header.GetMessages<AttributeInfoMessage>();

            if (attributeInfoMessages.Any())
            {
                if (attributeInfoMessages.Count() != 1)
                    throw new Exception("There may be only a single attribute info message.");

                var attributeInfoMessage = attributeInfoMessages.First();

                if (!context.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
                {
                    var (success, foundAttributeMessage) = await TryGetAttributeMessageFromAttributeInfoMessage(context, attributeInfoMessage, name).ConfigureAwait(false);

                    if (success)
                        return (true, foundAttributeMessage);
                }
            }
        }

        return (false, null);
    }

    // CONCURRENCY: an iterator, so the scope is created on the first MoveNext and disposed when the
    // enumerator is disposed - one scope per enumeration, not one per attribute. Each returned
    // NativeAttribute keeps the FILE-LEVEL context, because it outlives this enumeration and scopes
    // its own reads.
    //
    // Drains the async core one item at a time rather than buffering it, so that the synchronous
    // Attributes() stays as lazy as it has always been while both surfaces share one implementation.
    private IEnumerable<IH5Attribute> EnumerateAttributes()
    {
        using var scope = new NativeOperationScope(Context);

        var enumerator = EnumerateAttributeMessages(scope.Context, CancellationToken.None)
            .GetAsyncEnumerator();

        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                yield return new NativeAttribute(Context, enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Enumerates this object's attribute messages, compact ones first and then dense ones.
    /// </summary>
    /// <remarks>
    /// The single implementation behind both <c>Attributes()</c> and <c>AttributesAsync()</c>.
    /// <para>
    /// <paramref name="cancellationToken" /> is observed once per attribute. The driver reads
    /// underneath do not take a token, so cancellation is granular to an attribute rather than to an
    /// individual read - honest, and better than ignoring the token the public signature accepts.
    /// </para>
    /// </remarks>
    private async IAsyncEnumerable<AttributeMessage> EnumerateAttributeMessages(
        NativeReadContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var header = await GetHeader().ConfigureAwait(false);

        // AttributeInfoMessage is optional
        // AttributeMessage is optional
        // both may appear at the same time, or only of of them, or none of them
        // => do not use "if/else"

        // attributes are stored compactly
        foreach (var attributeMessage in header.GetMessages<AttributeMessage>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return attributeMessage;
        }

        // attributes are stored densely
        var attributeInfoMessages = header.GetMessages<AttributeInfoMessage>();

        if (attributeInfoMessages.Any())
        {
            if (attributeInfoMessages.Count() != 1)
                throw new Exception("There may be only a single attribute info message.");

            var attributeInfoMessage = attributeInfoMessages.First();

            if (!context.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
            {
                var denseMessages = EnumerateAttributeMessagesFromAttributeInfoMessage(
                    context,
                    attributeInfoMessage,
                    header.Address);

                await foreach (var attributeMessage in denseMessages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return attributeMessage;
                }
            }
        }
    }

    // Streams the b-tree records instead of draining them into a list first, which the synchronous
    // bridge here used to require. Safe because the record walk re-seeks before decoding each node
    // (BTree2Header.EnumerateRecords), so resolving a heap ID between two records - which does move
    // the cursor - cannot derail it. Same shape as the link-message enumeration in NativeGroup.
    private static async IAsyncEnumerable<AttributeMessage> EnumerateAttributeMessagesFromAttributeInfoMessage(
        NativeReadContext context,
        AttributeInfoMessage attributeInfoMessage,
        ulong headerAddress)
    {
        var fractalHeap = await attributeInfoMessage.FractalHeap(context).ConfigureAwait(false);

        await foreach (var record in attributeInfoMessage.EnumerateNameIndexRecords(context))
        {
            // TODO: duplicate1_of_3
            using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
            var heapId = await FractalHeapId.Construct(context, localDriver, fractalHeap).ConfigureAwait(false);

            yield return await heapId
                .Read(driver => AttributeMessage.Decode(context, headerAddress))
                .ConfigureAwait(false);
        }
    }

    // `Header` is read below as a cached field: the only caller (TryGetAttributeMessage) has already
    // awaited GetHeader(), so it cannot block here.
    private async ValueTask<(bool Success, AttributeMessage? AttributeMessage)> TryGetAttributeMessageFromAttributeInfoMessage(
        NativeReadContext context,
        AttributeInfoMessage attributeInfoMessage,
        string name)
    {
        var fractalHeap = await attributeInfoMessage.FractalHeap(context).ConfigureAwait(false);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var nameHash = ChecksumUtils.JenkinsLookup3(nameBytes);
        var candidate = default(AttributeMessage);

        var (success, record) = await attributeInfoMessage.TryFindNameIndexRecord(context, async record =>
        {
            // H5Abtree2.c (H5A__dense_btree2_name_compare, H5A__dense_fh_name_cmp)

            if (nameHash < record.NameHash)
            {
                return -1;
            }
            else if (nameHash > record.NameHash)
            {
                return 1;
            }
            else
            {
                // TODO: duplicate2_of_3
                using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
                var heapId = await FractalHeapId.Construct(context, localDriver, fractalHeap).ConfigureAwait(false);

                candidate = await heapId
                    .Read(driver => AttributeMessage.Decode(context, Header.Address))
                    .ConfigureAwait(false);

                // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                return string.CompareOrdinal(name, candidate.Name);
            }
        }).ConfigureAwait(false);

        if (success)
        {
            if (candidate is null)
                throw new Exception("This should never happen. Just to satisfy the compiler.");

            return (true, candidate);
        }

        return (false, null);
    }

    private uint GetReferenceCount()
    {
        var header1 = Header as ObjectHeader1;

        if (header1 is not null)
            return header1.ObjectReferenceCount;

        else
            return ObjectReferenceCount is null
                ? 1
                : ObjectReferenceCount.ReferenceCount;
    }

    #endregion
}
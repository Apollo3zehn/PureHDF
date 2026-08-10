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
    private protected ObjectHeader Header
    {
        get
        {
            var header = Volatile.Read(ref _header);

            if (header is null)
            {
                using var scope = new NativeOperationScope(Context);

                scope.Context.Driver.SeekRelativeToBaseAddress((long)Reference.Value);
                header = ObjectHeader.Construct(scope.Context).GetAwaiter().GetResult();

                Volatile.Write(ref _header, header);
            }

            return header;
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public IEnumerable<IH5Attribute> Attributes()
    {
        return EnumerateAttributes();
    }

    /// <inheritdoc />
    public Task<IEnumerable<IH5Attribute>> AttributesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
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
    public Task<IH5Attribute> AttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
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
    public Task<bool> AttributeExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
    }

    // NOTE (async propagation): this helper exclusively serves the synchronous
    // public API surface (Attribute()/AttributeExists()). It must itself become
    // async because it awaits TryGetAttributeMessageFromAttributeInfoMessage below
    // (an `out` parameter cannot coexist with `async`, CS1988), so it now returns a
    // tuple instead; the two public callers bridge once via GetAwaiter().GetResult().
    private async ValueTask<(bool Success, AttributeMessage? AttributeMessage)> TryGetAttributeMessage(
        NativeReadContext context,
        string name)
    {
        // get attribute from attribute message
        var attributeMessage = Header
            .GetMessages<AttributeMessage>()
            .FirstOrDefault(message => message.Name == name);

        if (attributeMessage is not null)
        {
            return (true, attributeMessage);
        }

        // get attribute from attribute info
        else
        {
            var attributeInfoMessages = Header.GetMessages<AttributeInfoMessage>();

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

    private IEnumerable<IH5Attribute> EnumerateAttributes()
    {
        // CONCURRENCY: this is an iterator, so the scope is created on the first MoveNext and
        // disposed when the enumerator is disposed - one scope per enumeration, not one per
        // attribute. Each returned NativeAttribute keeps the FILE-LEVEL context, because it outlives
        // this enumeration and scopes its own reads.
        using var scope = new NativeOperationScope(Context);
        var context = scope.Context;

        // AttributeInfoMessage is optional
        // AttributeMessage is optional
        // both may appear at the same time, or only of of them, or none of them
        // => do not use "if/else"

        // attributes are stored compactly
        var attributeMessages1 = Header.GetMessages<AttributeMessage>();

        foreach (var attributeMessage in attributeMessages1)
        {
            yield return new NativeAttribute(Context, attributeMessage);
        }

        // attributes are stored densely
        var attributeInfoMessages = Header.GetMessages<AttributeInfoMessage>();

        if (attributeInfoMessages.Any())
        {
            if (attributeInfoMessages.Count() != 1)
                throw new Exception("There may be only a single attribute info message.");

            var attributeInfoMessage = attributeInfoMessages.First();

            if (!context.Superblock.IsUndefinedAddress(attributeInfoMessage.BTree2NameIndexAddress))
            {
                var attributeMessages2 = EnumerateAttributeMessagesFromAttributeInfoMessage(context, attributeInfoMessage);

                foreach (var attributeMessage in attributeMessages2)
                {
                    yield return new NativeAttribute(Context, attributeMessage);
                }
            }
        }
    }

    private IEnumerable<AttributeMessage> EnumerateAttributeMessagesFromAttributeInfoMessage(
        NativeReadContext context,
        AttributeInfoMessage attributeInfoMessage)
    {
        // NOTE (async propagation): AttributeInfoMessage.EnumerateNameIndexRecords()/FractalHeap()
        // are now async (the former IAsyncEnumerable<T>, rule 8). This method must stay a
        // synchronous iterator (see report), so both are drained/bridged synchronously.
        var records = new List<BTree2Record08>();

        {
            var recordEnumerator = attributeInfoMessage
                .EnumerateNameIndexRecords(context)
                .GetAsyncEnumerator();

            try
            {
                while (recordEnumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    records.Add(recordEnumerator.Current);
                }
            }
            finally
            {
                recordEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        var fractalHeap = attributeInfoMessage.FractalHeap(context).GetAwaiter().GetResult();

        foreach (var record in records)
        {
            // TODO: duplicate1_of_3
            using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
            var heapId = FractalHeapId.Construct(context, localDriver, fractalHeap).GetAwaiter().GetResult();
            var message = heapId.Read(driver => AttributeMessage.Decode(context, Header.Address).GetAwaiter().GetResult());

            yield return message;
        }
    }

    // NOTE (async propagation): this is a private helper of TryGetAttributeMessage
    // above, not itself on the sync public API surface, so it is converted fully to
    // async (an `out` parameter cannot coexist with `async`, CS1988 — replaced with a
    // tuple return, matching FoundDelegate/TryFindRecord's own tuple-return shape).
    // The comparator lambda below must also become async: BTree2Header<T>.
    // TryFindRecord now takes Func<T, ValueTask<int>> (wave 4 addendum).
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

                // NOTE (async propagation): FractalHeapId.Read is a genuinely
                // synchronous abstract method (its subtype overrides take a `ref
                // List<BTree2Record01>` cache parameter, and `ref` cannot coexist
                // with `async` — CS1988, wave 4's stated known blocker). FractalHeapId
                // is owned by another agent's file, so its `Func<H5DriverBase, T>`
                // callback parameter cannot be changed to an async delegate from
                // here; this bridge is unavoidable without an out-of-scope edit.
                candidate = heapId.Read(driver => AttributeMessage.Decode(context, Header.Address).GetAwaiter().GetResult());

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
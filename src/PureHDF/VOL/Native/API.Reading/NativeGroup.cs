using System.Runtime.CompilerServices;
using System.Text;

namespace PureHDF.VOL.Native;

/// <summary>
/// An HDF5 group.
/// </summary>
public class NativeGroup : NativeObject, IH5Group
{
    #region Fields

    private readonly ObjectHeaderScratchPad? _scratchPad;

    #endregion

    #region Constructors

    internal NativeGroup(NativeReadContext context, NativeNamedReference reference)
       : base(context, reference)
    {
        _scratchPad = reference.ScratchPad;
    }

    internal NativeGroup(NativeReadContext context, NativeNamedReference reference, ObjectHeader header)
        : base(context, reference, header)
    {
        //
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public bool LinkExists(string path)
    {
        return LinkExists(path, default);
    }

    /// <inheritdoc />
    public Task<bool> LinkExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
    }

    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    public bool LinkExists(string path, H5LinkAccess linkAccess)
    {
        return InternalLinkExists(path, linkAccess);
    }

    /// <inheritdoc />
    public IH5Object Get(string path)
    {
        return Get(path, default);
    }

    /// <inheritdoc />
    public Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    public IH5Object Get(string path, H5LinkAccess linkAccess)
    {
        return InternalGet(path, linkAccess)
            .Dereference();
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <returns>The requested object.</returns>
    public IH5Object Get(NativeObjectReference1 reference)
    {
        if (reference.Equals(default))
            throw new Exception("The reference is invalid");

        return Get(reference, default);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The reference of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    public IH5Object Get(NativeObjectReference1 reference, H5LinkAccess linkAccess)
    {
        if (Reference.Value == reference.Value)
            return this;

        return InternalGet(reference, linkAccess)
            .Dereference();
    }

    /// <inheritdoc />
    public IEnumerable<IH5Object> Children()
    {
        return Children(default);
    }

    /// <inheritdoc />
    public Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("The native VOL connector does not support async read operations.");
    }

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>An enumerable of the available children.</returns>
    public IEnumerable<IH5Object> Children(H5LinkAccess linkAccess = default)
    {
        // NON-MECHANICAL (flagged, not guessed): EnumerateReferences is now
        // IAsyncEnumerable<NativeNamedReference> (rule 8), but Children() is public,
        // synchronous IH5Group API. Bridged with a manual blocking drain, mirroring the
        // precedent already established in NativeNamedReference.Dereference (out of
        // scope, unedited: ObjectHeader.Construct(context).GetAwaiter().GetResult()) to
        // avoid a breaking change to the public read surface.
        var references = new List<NativeNamedReference>();
        var enumerator = EnumerateReferences(linkAccess).GetAsyncEnumerator();

        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                references.Add(enumerator.Current);
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return references
            .Select(reference => reference.Dereference());
    }

    private bool InternalLinkExists(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return true;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Context.File.Reference : Reference;

        // Only the first iteration can reuse a group we already hold; every later segment names an
        // object not yet resolved, so this is cleared at the end of each pass.
        var group = isRooted ? null : this;

        for (int i = 0; i < segments.Length; i++)
        {
            if (group is null)
            {
                if (current.Dereference() is not NativeGroup dereferenced)
                    return false;

                group = dereferenced;
            }

            var (success, reference) = group.TryGetReference(segments[i], linkAccess).GetAwaiter().GetResult();

            if (!success)
                return false;

            current = reference;
            group = null;
        }

        return true;
    }

    internal NativeNamedReference InternalGet(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return Context.File.Reference;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Context.File.Reference : Reference;

        // Only the first iteration can reuse a group we already hold; every later segment names an
        // object not yet resolved, so this is cleared at the end of each pass.
        var group = isRooted ? null : this;

        for (int i = 0; i < segments.Length; i++)
        {
            if (group is null)
            {
                // TODO: Use cache to store dereferenced objects (as it is done in HsdsGroup.cs). That
                // would cover the remaining case - the intermediate segments of a deep path, and the
                // root of a rooted one - which still re-decode a header per lookup.
                if (current.Dereference() is not NativeGroup dereferenced)
                    throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");

                group = dereferenced;
            }

            var (success, reference) = group.TryGetReference(segments[i], linkAccess).GetAwaiter().GetResult();

            if (!success)
                throw new Exception($"Could not find part of the path '{path}'.");

            current = reference;
            group = null;
        }

        return current;
    }

    internal NativeNamedReference InternalGet(NativeObjectReference1 reference, H5LinkAccess linkAccess)
    {
        var alreadyVisted = new HashSet<ulong>();

        if (TryGetReference(reference, alreadyVisted, linkAccess, recursionLevel: 0, out var namedReference))
            return namedReference;

        else
            throw new Exception($"Could not find object for reference with value '{reference.Value:X}'.");
    }

    private async ValueTask<(bool Success, NativeNamedReference Reference)> TryGetReference(string name, H5LinkAccess linkAccess)
    {
        /* cached data */
        if (_scratchPad is not null)
        {
            /* According to the source code, scratch pad and symbol table message
             * are either both present or both absent and both point to the same
             * addresses.
             *
             * https://github.com/HDFGroup/hdf5/blob/55f4cc0caa69d65c505e926fb7b2568ab1a76c58/src/H5Gtest.c#L644-L649
             * https://github.com/HDFGroup/hdf5/blob/55f4cc0caa69d65c505e926fb7b2568ab1a76c58/src/H5Gtest.c#L698-L703
             *
             * This suggests that the image in PureHDF/issues/25 is missing due to
             * an invalid file.
             */
            var localHeap = await _scratchPad.GetLocalHeap().ConfigureAwait(false);

            var (success, userData) = await (await _scratchPad.GetBTree1(DecodeGroupKey).ConfigureAwait(false))
                .TryFindUserData<BTree1SymbolTableUserData>(
                    (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                    (address, _) => NodeFound(localHeap, name, address))
                .ConfigureAwait(false);

            if (success)
            {
                var namedReference = await GetObjectReferencesForSymbolTableEntry(localHeap, userData.SymbolTableEntry, linkAccess).ConfigureAwait(false);
                return (true, namedReference);
            }
        }
        else
        {
            var symbolTableHeaderMessages = Header.GetMessages<SymbolTableMessage>();

            if (symbolTableHeaderMessages.Any())
            {
                /* Original approach.
                 * IV.A.2.r.: The Symbol Table Message
                 * Required for "old style" groups; may not be repeated. */

                if (symbolTableHeaderMessages.Count() != 1)
                    throw new Exception("There may be only a single symbol table header message.");

                var smessage = symbolTableHeaderMessages.First();
                var localHeap = await smessage.GetLocalHeap().ConfigureAwait(false);

                var (success, userData) = await (await smessage.GetBTree1(DecodeGroupKey).ConfigureAwait(false))
                    .TryFindUserData<BTree1SymbolTableUserData>(
                        (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                        (address, _) => NodeFound(localHeap, name, address))
                    .ConfigureAwait(false);

                if (success)
                {
                    var namedReference = await GetObjectReferencesForSymbolTableEntry(localHeap, userData.SymbolTableEntry, linkAccess).ConfigureAwait(false);
                    return (true, namedReference);
                }
            }
            else
            {
                var linkInfoMessages = Header.GetMessages<LinkInfoMessage>();

                if (linkInfoMessages.Any())
                {
                    if (linkInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single link info message.");

                    var lmessage = linkInfoMessages.First();

                    /* New (1.8) indexed format (in combination with Group Info Message)
                     * IV.A.2.c. The Link Info Message
                     * Optional; may not be repeated. */
                    if (!Context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                    {
                        var (found, linkMessage) = await TryGetLinkMessageFromLinkInfoMessage(lmessage, name).ConfigureAwait(false);

                        if (found)
                        {
                            return (true, GetObjectReference(linkMessage!, linkAccess));
                        }
                    }
                    /* New (1.8) compact format
                     * IV.A.2.g. The Link Message
                     * A group is storing its links compactly when the fractal heap address
                     * in the Link Info Message is set to the "undefined address" value. */
                    else
                    {
                        var linkMessage = Header
                            .GetMessages<LinkMessage>()
                            .FirstOrDefault(message => message.LinkName == name);

                        if (linkMessage is not null)
                        {
                            return (true, GetObjectReference(linkMessage, linkAccess));
                        }
                    }
                }
                else
                {
                    throw new Exception("No link information found in object header.");
                }
            }
        }

        return (false, default);
    }

    // TODO this should make use of the cache to avoid recursively visiting all node (as soon as the cache is implemented)
    internal bool TryGetReference(NativeObjectReference1 reference, HashSet<ulong> alreadyVisited, H5LinkAccess linkAccess, int recursionLevel, out NativeNamedReference namedReference)
    {
        // similar to H5Gint.c (H5G_visit)
        if (recursionLevel >= 100)
            throw new Exception("Too much recursion.");

        bool skip = false;
        namedReference = default;

        /* If its ref count is > 1, we add it to the list of visited objects
         * (because it could come up again during traversal) */
        if (ReferenceCount > 1)
        {
            if (alreadyVisited.Contains(Reference.Value))
                skip = true;
            else
                alreadyVisited.Add(Reference.Value);
        }

        if (!skip)
        {
            // NON-MECHANICAL (flagged, not guessed): EnumerateReferences is now
            // IAsyncEnumerable<NativeNamedReference> (rule 8). This method stays
            // synchronous (bool + out param, matching its own recursive contract and
            // the public sync boundary above it), so the enumeration is drained with a
            // blocking loop instead — same pattern as Children() above.
            var references = new List<NativeNamedReference>();
            var enumerator = EnumerateReferences(linkAccess).GetAsyncEnumerator();

            try
            {
                while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    references.Add(enumerator.Current);
                }
            }
            finally
            {
                enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            namedReference = references
                .FirstOrDefault(current => current.Value == reference.Value);

            if (namedReference.Name is not null /* if struct value is not equal to default */)
            {
                return true;
            }

            else
            {
                // search childs for reference
                foreach (var childReference in references)
                {
                    var group = childReference.Dereference() as NativeGroup;

                    if (group is not null)
                    {
                        if (group.TryGetReference(reference, alreadyVisited, linkAccess, recursionLevel + 1, out namedReference))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private async IAsyncEnumerable<NativeNamedReference> EnumerateReferences(H5LinkAccess linkAccess)
    {
        // https://support.hdfgroup.org/HDF5/doc/RM/RM_H5G.html
        // section "Group implementations in HDF5"

        /* cached data */
        if (_scratchPad is not null)
        {
            /* According to the source code, scratch pad and symbol table message
             * are either both present or both absent and both point to the same
             * addresses.
             *
             * https://github.com/HDFGroup/hdf5/blob/55f4cc0caa69d65c505e926fb7b2568ab1a76c58/src/H5Gtest.c#L644-L649
             * https://github.com/HDFGroup/hdf5/blob/55f4cc0caa69d65c505e926fb7b2568ab1a76c58/src/H5Gtest.c#L698-L703
             *
             * This suggests that the image in PureHDF/issues/25 is missing due to
             * an invalid file.
             */
            var localHeap = await _scratchPad.GetLocalHeap().ConfigureAwait(false);
            var btree1 = await _scratchPad.GetBTree1(DecodeGroupKey).ConfigureAwait(false);

            await foreach (var node in EnumerateSymbolTableNodes(btree1))
            {
                foreach (var entry in node.GroupEntries)
                {
                    yield return await GetObjectReferencesForSymbolTableEntry(localHeap, entry, linkAccess).ConfigureAwait(false);
                }
            }
        }
        else
        {
            var symbolTableHeaderMessages = Header.GetMessages<SymbolTableMessage>();

            if (symbolTableHeaderMessages.Any())
            {
                /* Original approach.
                 * IV.A.2.r.: The Symbol Table Message
                 * Required for "old style" groups; may not be repeated. */

                if (symbolTableHeaderMessages.Count() != 1)
                    throw new Exception("There may be only a single symbol table header message.");

                var smessage = symbolTableHeaderMessages.First();
                var localHeap = await smessage.GetLocalHeap().ConfigureAwait(false);
                var btree1 = await smessage.GetBTree1(DecodeGroupKey).ConfigureAwait(false);

                await foreach (var node in EnumerateSymbolTableNodes(btree1))
                {
                    foreach (var entry in node.GroupEntries)
                    {
                        yield return await GetObjectReferencesForSymbolTableEntry(localHeap, entry, linkAccess).ConfigureAwait(false);
                    }
                }
            }

            else
            {
                var linkInfoMessages = Header.GetMessages<LinkInfoMessage>();

                if (linkInfoMessages.Any())
                {
                    if (linkInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single link info message.");

                    var lmessage = linkInfoMessages.First();

                    /* New (1.8) indexed format (in combination with Group Info Message)
                     * IV.A.2.c. The Link Info Message
                     * Optional; may not be repeated. */
                    if (!Context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                    {
                        // build links
                        await foreach (var linkMessage in EnumerateLinkMessagesFromLinkInfoMessage(lmessage))
                        {
                            yield return GetObjectReference(linkMessage, linkAccess);
                        }
                    }

                    /* New (1.8) compact format
                     * IV.A.2.g. The Link Message
                     * A group is storing its links compactly when the fractal heap address
                     * in the Link Info Message is set to the "undefined address" value. */
                    else
                    {
                        // build links
                        foreach (var linkMessage in Header.GetMessages<LinkMessage>())
                        {
                            yield return GetObjectReference(linkMessage, linkAccess);
                        }
                    }
                }
                else
                {
                    throw new Exception("No link information found in object header.");
                }
            }
        }
    }

    #endregion

    #region Link Message

    private async IAsyncEnumerable<LinkMessage> EnumerateLinkMessagesFromLinkInfoMessage(LinkInfoMessage infoMessage)
    {
        var fractalHeap = await infoMessage.FractalHeap().ConfigureAwait(false);
        var btree2NameIndex = await infoMessage.BTree2NameIndex().ConfigureAwait(false);

        // local cache: indirectly accessed, non-filtered
        List<BTree2Record01>? record01Cache = null;

        await foreach (var record in btree2NameIndex.EnumerateRecords())
        {
            using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
            var heapId = await FractalHeapId.Construct(Context, localDriver, fractalHeap).ConfigureAwait(false);

            yield return await heapId.Read(driver =>
            {
                var message = LinkMessage.Decode(Context);
                return message;
            }, ref record01Cache).ConfigureAwait(false);
        }
    }

    // NOTE (async propagation, rule 4 analog): `out LinkMessage? linkMessage` cannot
    // coexist with `async` (CS1988), so the out parameter became a tuple return,
    // matching the pattern used elsewhere in this wave (see BTree1Node.TryFindUserData,
    // BTree2Header.TryFindRecord). Flagging as a shape change.
    private async ValueTask<(bool Success, LinkMessage? LinkMessage)> TryGetLinkMessageFromLinkInfoMessage(
        LinkInfoMessage linkInfoMessage,
        string name)
    {
        var fractalHeap = await linkInfoMessage.FractalHeap().ConfigureAwait(false);
        var btree2NameIndex = await linkInfoMessage.BTree2NameIndex().ConfigureAwait(false);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var nameHash = ChecksumUtils.JenkinsLookup3(nameBytes);
        var candidate = default(LinkMessage);

        // ASYNC PROPAGATION (Wave 4 addendum): BTree2Header<T>.TryFindRecord's comparator
        // (out of scope, BTree2Header.cs) is now `Func<T, ValueTask<int>>`, so this
        // comparator is an async lambda and the former `.GetAwaiter().GetResult()`
        // bridges around FractalHeapId.Construct and LinkMessage.Decode are replaced with
        // await — both are already async (see FractalHeapId.cs / LinkMessage.Reading.cs,
        // out of scope, unedited). FractalHeapId.Read<T> itself stays synchronous (its
        // `ref List<BTree2Record01>` cache parameter makes it CS1988-ineligible for async,
        // per the wave 4 known blocker); T is inferred here as the unawaited
        // `ValueTask<LinkMessage>` returned by the delegate, which is then awaited outside
        // the (still synchronous) Read call — the same pattern already used in
        // EnumerateLinkMessagesFromLinkInfoMessage above.
        var (success, _) = await btree2NameIndex.TryFindRecord(async record =>
        {
            // H5Gbtree2.c (H5G__dense_btree2_name_compare, H5G__dense_fh_name_cmp)

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
                // TODO: duplicate3_of_3
                using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
                var heapId = await FractalHeapId.Construct(Context, localDriver, fractalHeap).ConfigureAwait(false);
                candidate = await heapId.Read(driver => LinkMessage.Decode(Context)).ConfigureAwait(false);

                // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                return string.CompareOrdinal(name, candidate.LinkName);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NativeNamedReference GetObjectReference(LinkMessage linkMessage, H5LinkAccess linkAccess)
    {
        return linkMessage.LinkInfo switch
        {
            HardLinkInfo hard => new NativeNamedReference(linkMessage.LinkName, hard.HeaderAddress, Context.File),

            SoftLinkInfo _ => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(linkAccess),

            ExternalLinkInfo _ => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(linkAccess),

            _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
        };
    }

    #endregion

    #region Symbol Table

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<NativeNamedReference> GetObjectReferencesForSymbolTableEntry(LocalHeap heap, SymbolTableEntry entry, H5LinkAccess linkAccess)
    {
        var name = await heap.GetObjectName(entry.LinkNameOffset).ConfigureAwait(false);
        var reference = new NativeNamedReference(name, entry.HeaderAddress, Context.File);

        return entry.ScratchPad switch
        {
            ObjectHeaderScratchPad objectScratch => AddScratchPad(reference, objectScratch),

            SymbolicLinkScratchPad linkScratch => new SymbolicLink(
                name,
                await heap.GetObjectName(linkScratch.LinkValueOffset).ConfigureAwait(false),
                this,
                Context.File).GetTarget(linkAccess),

            _ when !Context.Superblock.IsUndefinedAddress(entry.HeaderAddress) => reference,

            _ => throw new Exception("Unknown object type.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NativeNamedReference AddScratchPad(NativeNamedReference reference, ObjectHeaderScratchPad scratchPad)
    {
        reference.ScratchPad = scratchPad;
        return reference;
    }

    private async IAsyncEnumerable<SymbolTableNode> EnumerateSymbolTableNodes(BTree1Node<BTree1GroupKey> btree1)
    {
        await foreach (var node in btree1.EnumerateNodes())
        {
            foreach (var address in node.ChildAddresses)
            {
                Context.Driver.SeekRelativeToBaseAddress((long)address);
                yield return await SymbolTableNode.Decode(Context).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Callbacks

    // ASYNC PROPAGATION: BTree1Node<T>'s `compare3` parameter is now
    // `Func<T, T, ValueTask<int>>` (BTree1Node.cs, out of scope but already converted),
    // so the former `.GetAwaiter().GetResult()` bridge is no longer needed here — this
    // callback is awaited by BTree1Node<T>.LocateRecord itself.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<int> NodeCompare3(LocalHeap localHeap, string name, BTree1GroupKey leftKey, BTree1GroupKey rightKey)
    {
        // H5Gnode.c (H5G_node_cmp3)

        /* left side */
        var leftName = await localHeap.GetObjectName(leftKey.LocalHeapByteOffset).ConfigureAwait(false);

        if (string.CompareOrdinal(name, leftName) <= 0)
        {
            return -1;
        }
        else
        {
            /* right side */
            var rightName = await localHeap.GetObjectName(rightKey.LocalHeapByteOffset).ConfigureAwait(false);

            if (string.CompareOrdinal(name, rightName) > 0)
            {
                return 1;
            }
        }

        return 0;
    }

    // ASYNC PROPAGATION: `FoundDelegate<T, TUserData>` (BTree1Node.cs, out of scope but
    // already converted) is now `ValueTask<(bool Success, TUserData UserData)> (ulong
    // address, T leftNode)` — the `out` parameter became a tuple return because `out`
    // cannot coexist with `async` (CS1988). Callers curry `localHeap`/`name` via a
    // lambda (see TryGetReference) matching this method's remaining parameters.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<(bool Success, BTree1SymbolTableUserData UserData)> NodeFound(LocalHeap localHeap, string name, ulong address)
    {
        // H5Gnode.c (H5G__node_found)
        uint low = 0, index = 0, high;
        int cmp = 1;

        /*
         * Load the symbol table node for exclusive access.
         */
        Context.Driver.SeekRelativeToBaseAddress((long)address);
        var symbolTableNode = await SymbolTableNode.Decode(Context).ConfigureAwait(false);

        /*
         * Binary search.
         */
        high = symbolTableNode.SymbolCount;

        while (low < high && cmp != 0)
        {
            index = (low + high) / 2;

            var linkNameOffset = symbolTableNode.GroupEntries[(int)index].LinkNameOffset;
            var currentName = await localHeap.GetObjectName(linkNameOffset).ConfigureAwait(false);
            cmp = string.CompareOrdinal(name, currentName);

            if (cmp < 0)
                high = index;
            else
                low = index + 1;
        }

        if (cmp != 0)
            return (false, default);

        var userData = new BTree1SymbolTableUserData(
            SymbolTableEntry: symbolTableNode.GroupEntries[(int)index]
        );

        return (true, userData);
    }

    // ASYNC PROPAGATION: `BTree1Node<T>.DecodeKey`/`Decode(...)` (BTree1Node.cs, out of
    // scope but already converted) now take `Func<ValueTask<T>>`, and
    // `SymbolTableMessage.GetBTree1` (out of scope but already converted) matches suit.
    // `BTree1GroupKey.Decode` (BTree1Types.cs, out of scope, already async) is simply
    // awaited instead of bridged.
    //
    // OUT-OF-SCOPE GAP: `ObjectHeaderScratchPad.GetBTree1` (ScratchPadTypes.cs) has not
    // been updated yet and still declares a synchronous `Func<BTree1GroupKey> decodeKey`
    // parameter, unlike its sibling `SymbolTableMessage.GetBTree1`. Passing this now-async
    // `DecodeGroupKey` to `_scratchPad.GetBTree1(DecodeGroupKey)` (see TryGetReference /
    // EnumerateReferences above) requires that file to be updated the same way; flagged
    // in the report rather than edited here.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<BTree1GroupKey> DecodeGroupKey()
    {
        return await BTree1GroupKey.Decode(Context).ConfigureAwait(false);
    }

    #endregion
}
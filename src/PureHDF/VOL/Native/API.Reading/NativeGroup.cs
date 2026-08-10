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
        return LinkExistsAsync(path, default, cancellationToken);
    }

    /// <summary>
    /// Checks if the link with the specified <paramref name="path"/> exist.
    /// </summary>
    /// <param name="path">The path of the link.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>A boolean which indicates if the link exists.</returns>
    public async Task<bool> LinkExistsAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        if (path == "/")
            return true;

        using var scope = new NativeOperationScope(Context);

        var (success, _, _) = await TryWalkPath(scope.Context, path, linkAccess, cancellationToken).ConfigureAwait(false);

        return success;
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
        return GetAsync(path, default, cancellationToken);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>The requested object.</returns>
    public async Task<IH5Object> GetAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        // CONCURRENCY: ONE scope for the entire call, exactly as the synchronous Get does.
        using var scope = new NativeOperationScope(Context);

        var reference = await InternalGet(scope.Context, path, linkAccess, cancellationToken).ConfigureAwait(false);

        return await reference.DereferenceAsync(scope.Context).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the object that is at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the object.</param>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>The requested object.</returns>
    public IH5Object Get(string path, H5LinkAccess linkAccess)
    {
        // CONCURRENCY: ONE scope for the entire call - the path walk below visits a group per
        // segment and dereferences each, and all of it must run on the same isolated driver. See the
        // note on InternalGet for why the recursion does not create a scope per step.
        using var scope = new NativeOperationScope(Context);

        // Blocks once, at the public boundary, rather than at every step of the walk. GetAsync above
        // runs the same code path without blocking at all.
        return InternalGet(scope.Context, path, linkAccess)
            .GetAwaiter()
            .GetResult()
            .Dereference(scope.Context);
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

        // CONCURRENCY: one scope for the whole (recursive) search - see Get(string, ...) above.
        using var scope = new NativeOperationScope(Context);

        return InternalGet(scope.Context, reference, linkAccess)
            .Dereference(scope.Context);
    }

    /// <inheritdoc />
    public IEnumerable<IH5Object> Children()
    {
        return Children(default);
    }

    /// <inheritdoc />
    public Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        return ChildrenAsync(default, cancellationToken);
    }

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <param name="cancellationToken">A token to cancel the current operation.</param>
    /// <returns>An enumerable of the available children.</returns>
    public async Task<IEnumerable<IH5Object>> ChildrenAsync(H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        // Materializes rather than streaming, because IH5Group promises Task<IEnumerable<...>> and not
        // IAsyncEnumerable<...>. The synchronous Children() below stays lazy.
        using var scope = new NativeOperationScope(Context);

        var children = new List<IH5Object>();

        await foreach (var reference in EnumerateReferences(scope.Context, linkAccess, cancellationToken))
        {
            children.Add(await reference.DereferenceAsync(scope.Context).ConfigureAwait(false));
        }

        return children;
    }

    /// <summary>
    /// Gets an enumerable of the available children using the optionally specified <paramref name="linkAccess"/>.
    /// </summary>
    /// <param name="linkAccess">The link access properties.</param>
    /// <returns>An enumerable of the available children.</returns>
    public IEnumerable<IH5Object> Children(H5LinkAccess linkAccess = default)
    {
        return EnumerateChildren(linkAccess);
    }

    // CONCURRENCY: an iterator, so the scope is created on the first MoveNext and disposed when the
    // enumerator is disposed - ONE scope for the whole enumeration, not one per link visited. Each
    // GetEnumerator() call builds its own state machine and therefore its own scope, so two threads
    // enumerating the same returned IEnumerable do not share a driver.
    //
    // Side effect worth naming: the link drain used to run eagerly, inside the Children() call itself.
    // It now runs on the first MoveNext, together with everything else in the scope, so a malformed
    // group throws when the result is enumerated rather than when it is requested. Children() returns
    // IEnumerable and nothing in the tree depends on the earlier timing.
    // Drains the async enumeration one item at a time rather than buffering it, so the synchronous
    // Children() stays as lazy as it has always been while both surfaces share one implementation.
    private IEnumerable<IH5Object> EnumerateChildren(H5LinkAccess linkAccess)
    {
        using var scope = new NativeOperationScope(Context);
        var context = scope.Context;

        var enumerator = EnumerateReferences(context, linkAccess, CancellationToken.None)
            .GetAsyncEnumerator();

        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                yield return enumerator.Current.Dereference(context);
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // Still buffers, unlike EnumerateChildren above, because its caller
    // (TryGetReference(..., out ...)) needs the full list twice: once to look for a match and once to
    // recurse into every child.
    private List<NativeNamedReference> DrainReferences(NativeReadContext context, H5LinkAccess linkAccess)
    {
        var references = new List<NativeNamedReference>();
        var enumerator = EnumerateReferences(context, linkAccess, CancellationToken.None).GetAsyncEnumerator();

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

        return references;
    }

    private bool InternalLinkExists(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return true;

        // CONCURRENCY: one scope for the whole path walk - see Get(string, ...) above.
        using var scope = new NativeOperationScope(Context);

        // Blocks once, at the public boundary. LinkExistsAsync runs the same walk without blocking.
        var (success, _, _) = TryWalkPath(scope.Context, path, linkAccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return success;
    }

    // CONCURRENCY: the scope-creating overload, for callers that are NOT already inside an operation
    // on this file. SymbolicLink.GetTarget uses it for the external-file branch, where the target
    // group belongs to a DIFFERENT NativeFile and must be walked on that file's own driver - the
    // current operation's driver reads the wrong file entirely.
    internal async ValueTask<NativeNamedReference> InternalGet(string path, H5LinkAccess linkAccess)
    {
        using var scope = new NativeOperationScope(Context);

        return await InternalGet(scope.Context, path, linkAccess, CancellationToken.None).ConfigureAwait(false);
    }

    // CONCURRENCY: takes the enclosing operation's context instead of creating a scope, which is what
    // keeps the path walk (and the soft-link resolution it can re-enter through
    // SymbolicLink.GetTarget) on a single driver rather than one per segment.
    internal async ValueTask<NativeNamedReference> InternalGet(
        NativeReadContext context,
        string path,
        H5LinkAccess linkAccess,
        CancellationToken cancellationToken = default)
    {
        if (path == "/")
            return Context.File.Reference;

        var (success, reference, failedSegment) = await TryWalkPath(context, path, linkAccess, cancellationToken)
            .ConfigureAwait(false);

        if (!success)
        {
            // Distinguishes the two ways a walk fails, which the caller cannot tell apart from a
            // false: a segment that resolved but is not a group, versus a segment that is simply
            // absent.
            if (failedSegment is not null)
                throw new Exception($"Path segment '{failedSegment}' is not a group.");

            throw new Exception($"Could not find part of the path '{path}'.");
        }

        return reference;
    }

    /// <summary>
    /// Walks <paramref name="path" /> segment by segment, reporting whether it resolved rather than
    /// throwing, so that LinkExists and Get can share one implementation.
    /// </summary>
    /// <remarks>
    /// <c>FailedSegment</c> is set only for the "resolved but is not a group" failure; a missing
    /// segment leaves it null. Get turns the two into different exceptions, LinkExists ignores both.
    /// </remarks>
    private async ValueTask<(bool Success, NativeNamedReference Reference, string? FailedSegment)> TryWalkPath(
        NativeReadContext context,
        string path,
        H5LinkAccess linkAccess,
        CancellationToken cancellationToken)
    {
        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Context.File.Reference : Reference;

        // Only the first iteration can reuse a group we already hold; every later segment names an
        // object not yet resolved, so this is cleared at the end of each pass.
        var group = isRooted ? null : this;

        for (int i = 0; i < segments.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (group is null)
            {
                // TODO: Use cache to store dereferenced objects (as it is done in HsdsGroup.cs). That
                // would cover the remaining case - the intermediate segments of a deep path, and the
                // root of a rooted one - which still re-decode a header per lookup.
                if (await current.DereferenceAsync(context).ConfigureAwait(false) is not NativeGroup dereferenced)
                    return (false, default, i == 0 ? path : segments[i - 1]);

                group = dereferenced;
            }

            // CONCURRENCY: an external link makes the walk cross into another file - continue on that
            // file's driver, and on this operation's driver while it stays in this file.
            using var step = NativeOperationScope.ForFile(group.Context.File, context);

            var (found, reference) = await group
                .TryGetReference(step.Context, segments[i], linkAccess)
                .ConfigureAwait(false);

            if (!found)
                return (false, default, null);

            current = reference;
            group = null;
        }

        return (true, current, null);
    }

    internal NativeNamedReference InternalGet(NativeReadContext context, NativeObjectReference1 reference, H5LinkAccess linkAccess)
    {
        var alreadyVisted = new HashSet<ulong>();

        if (TryGetReference(context, reference, alreadyVisted, linkAccess, recursionLevel: 0, out var namedReference))
            return namedReference;

        else
            throw new Exception($"Could not find object for reference with value '{reference.Value:X}'.");
    }

    private async ValueTask<(bool Success, NativeNamedReference Reference)> TryGetReference(
        NativeReadContext context,
        string name,
        H5LinkAccess linkAccess)
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
            var localHeap = await _scratchPad.GetLocalHeap(context).ConfigureAwait(false);

            var (success, userData) = await (await _scratchPad.GetBTree1(context, DecodeGroupKey).ConfigureAwait(false))
                .TryFindUserData<BTree1SymbolTableUserData>(
                    context,
                    DecodeGroupKey,
                    (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                    (address, _) => NodeFound(context, localHeap, name, address))
                .ConfigureAwait(false);

            if (success)
            {
                var namedReference = await GetObjectReferencesForSymbolTableEntry(context, localHeap, userData.SymbolTableEntry, linkAccess).ConfigureAwait(false);
                return (true, namedReference);
            }
        }
        else
        {
            // Awaited once so that every `Header` read below is a cached field read - see
            // NativeObject.GetHeader.
            var header = await GetHeader().ConfigureAwait(false);
            var symbolTableHeaderMessages = header.GetMessages<SymbolTableMessage>();

            if (symbolTableHeaderMessages.Any())
            {
                /* Original approach.
                 * IV.A.2.r.: The Symbol Table Message
                 * Required for "old style" groups; may not be repeated. */

                if (symbolTableHeaderMessages.Count() != 1)
                    throw new Exception("There may be only a single symbol table header message.");

                var smessage = symbolTableHeaderMessages.First();
                var localHeap = await smessage.GetLocalHeap(context).ConfigureAwait(false);

                var (success, userData) = await (await smessage.GetBTree1(context, DecodeGroupKey).ConfigureAwait(false))
                    .TryFindUserData<BTree1SymbolTableUserData>(
                        context,
                        DecodeGroupKey,
                        (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                        (address, _) => NodeFound(context, localHeap, name, address))
                    .ConfigureAwait(false);

                if (success)
                {
                    var namedReference = await GetObjectReferencesForSymbolTableEntry(context, localHeap, userData.SymbolTableEntry, linkAccess).ConfigureAwait(false);
                    return (true, namedReference);
                }
            }
            else
            {
                var linkInfoMessages = header.GetMessages<LinkInfoMessage>();

                if (linkInfoMessages.Any())
                {
                    if (linkInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single link info message.");

                    var lmessage = linkInfoMessages.First();

                    /* New (1.8) indexed format (in combination with Group Info Message)
                     * IV.A.2.c. The Link Info Message
                     * Optional; may not be repeated. */
                    if (!context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                    {
                        var (found, linkMessage) = await TryGetLinkMessageFromLinkInfoMessage(context, lmessage, name).ConfigureAwait(false);

                        if (found)
                        {
                            var namedReference = await GetObjectReference(context, linkMessage!, linkAccess).ConfigureAwait(false);
                            return (true, namedReference);
                        }
                    }
                    /* New (1.8) compact format
                     * IV.A.2.g. The Link Message
                     * A group is storing its links compactly when the fractal heap address
                     * in the Link Info Message is set to the "undefined address" value. */
                    else
                    {
                        var linkMessage = header
                            .GetMessages<LinkMessage>()
                            .FirstOrDefault(message => message.LinkName == name);

                        if (linkMessage is not null)
                        {
                            var namedReference = await GetObjectReference(context, linkMessage, linkAccess).ConfigureAwait(false);
                            return (true, namedReference);
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
    // CONCURRENCY: `context` is threaded through the recursion rather than scoped here, so a deep
    // search runs on ONE driver instead of allocating one per level. The caller
    // (Get(NativeObjectReference1, ...) / InternalGet) owns the scope.
    internal bool TryGetReference(NativeReadContext context, NativeObjectReference1 reference, HashSet<ulong> alreadyVisited, H5LinkAccess linkAccess, int recursionLevel, out NativeNamedReference namedReference)
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
            var references = DrainReferences(context, linkAccess);

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
                    var group = childReference.Dereference(context) as NativeGroup;

                    if (group is not null)
                    {
                        // CONCURRENCY: a child reached through an external link lives in another file
                        // - recurse on that file's driver, and on this operation's driver otherwise.
                        using var step = NativeOperationScope.ForFile(group.Context.File, context);

                        if (group.TryGetReference(step.Context, reference, alreadyVisited, linkAccess, recursionLevel + 1, out namedReference))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Enumerates this group's links, whichever of the four storage forms it uses.
    /// </summary>
    /// <remarks>
    /// The single implementation behind both <c>Children()</c> and <c>ChildrenAsync()</c>.
    /// <para>
    /// <paramref name="cancellationToken" /> is observed once per link. The driver reads underneath do
    /// not take a token, so cancellation is granular to a link rather than to an individual read.
    /// </para>
    /// </remarks>
    private async IAsyncEnumerable<NativeNamedReference> EnumerateReferences(
        NativeReadContext context,
        H5LinkAccess linkAccess,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
            var localHeap = await _scratchPad.GetLocalHeap(context).ConfigureAwait(false);
            var btree1 = await _scratchPad.GetBTree1(context, DecodeGroupKey).ConfigureAwait(false);

            await foreach (var node in EnumerateSymbolTableNodes(context, btree1))
            {
                foreach (var entry in node.GroupEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return await GetObjectReferencesForSymbolTableEntry(context, localHeap, entry, linkAccess).ConfigureAwait(false);
                }
            }
        }
        else
        {
            // Awaited here rather than at the top of the method, because the scratch-pad branch above
            // resolves its links without ever touching the object header and must not be made to
            // decode one.
            var header = await GetHeader().ConfigureAwait(false);
            var symbolTableHeaderMessages = header.GetMessages<SymbolTableMessage>();

            if (symbolTableHeaderMessages.Any())
            {
                /* Original approach.
                 * IV.A.2.r.: The Symbol Table Message
                 * Required for "old style" groups; may not be repeated. */

                if (symbolTableHeaderMessages.Count() != 1)
                    throw new Exception("There may be only a single symbol table header message.");

                var smessage = symbolTableHeaderMessages.First();
                var localHeap = await smessage.GetLocalHeap(context).ConfigureAwait(false);
                var btree1 = await smessage.GetBTree1(context, DecodeGroupKey).ConfigureAwait(false);

                await foreach (var node in EnumerateSymbolTableNodes(context, btree1))
                {
                    foreach (var entry in node.GroupEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        yield return await GetObjectReferencesForSymbolTableEntry(context, localHeap, entry, linkAccess).ConfigureAwait(false);
                    }
                }
            }

            else
            {
                var linkInfoMessages = header.GetMessages<LinkInfoMessage>();

                if (linkInfoMessages.Any())
                {
                    if (linkInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single link info message.");

                    var lmessage = linkInfoMessages.First();

                    /* New (1.8) indexed format (in combination with Group Info Message)
                     * IV.A.2.c. The Link Info Message
                     * Optional; may not be repeated. */
                    if (!context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                    {
                        // build links
                        await foreach (var linkMessage in EnumerateLinkMessagesFromLinkInfoMessage(context, lmessage))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            yield return await GetObjectReference(context, linkMessage, linkAccess).ConfigureAwait(false);
                        }
                    }

                    /* New (1.8) compact format
                     * IV.A.2.g. The Link Message
                     * A group is storing its links compactly when the fractal heap address
                     * in the Link Info Message is set to the "undefined address" value. */
                    else
                    {
                        // build links
                        foreach (var linkMessage in header.GetMessages<LinkMessage>())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            yield return await GetObjectReference(context, linkMessage, linkAccess).ConfigureAwait(false);
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

    private async IAsyncEnumerable<LinkMessage> EnumerateLinkMessagesFromLinkInfoMessage(
        NativeReadContext context,
        LinkInfoMessage infoMessage)
    {
        var fractalHeap = await infoMessage.FractalHeap(context).ConfigureAwait(false);

        await foreach (var record in infoMessage.EnumerateNameIndexRecords(context))
        {
            using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
            var heapId = await FractalHeapId.Construct(context, localDriver, fractalHeap).ConfigureAwait(false);

            yield return await heapId
                .Read(driver => LinkMessage.Decode(context))
                .ConfigureAwait(false);
        }
    }

    // NOTE (async propagation, rule 4 analog): `out LinkMessage? linkMessage` cannot
    // coexist with `async` (CS1988), so the out parameter became a tuple return,
    // matching the pattern used elsewhere in this wave (see BTree1Node.TryFindUserData,
    // BTree2Header.TryFindRecord). Flagging as a shape change.
    private async ValueTask<(bool Success, LinkMessage? LinkMessage)> TryGetLinkMessageFromLinkInfoMessage(
        NativeReadContext context,
        LinkInfoMessage linkInfoMessage,
        string name)
    {
        var fractalHeap = await linkInfoMessage.FractalHeap(context).ConfigureAwait(false);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var nameHash = ChecksumUtils.JenkinsLookup3(nameBytes);
        var candidate = default(LinkMessage);

        var (success, _) = await linkInfoMessage.TryFindNameIndexRecord(context, async record =>
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
                var heapId = await FractalHeapId.Construct(context, localDriver, fractalHeap).ConfigureAwait(false);
                candidate = await heapId.Read(driver => LinkMessage.Decode(context)).ConfigureAwait(false);

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

    // Async only because of the two symbolic-link cases, which resolve by walking a path. A hard link
    // - the common case by far - completes synchronously and allocates no state machine, which is why
    // this stays non-async with an explicit ValueTask.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<NativeNamedReference> GetObjectReference(NativeReadContext context, LinkMessage linkMessage, H5LinkAccess linkAccess)
    {
        return linkMessage.LinkInfo switch
        {
            HardLinkInfo hard => new ValueTask<NativeNamedReference>(
                new NativeNamedReference(linkMessage.LinkName, hard.HeaderAddress, Context.File)),

            SoftLinkInfo _ => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(context, linkAccess),

            ExternalLinkInfo _ => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(context, linkAccess),

            _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
        };
    }

    #endregion

    #region Symbol Table

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async ValueTask<NativeNamedReference> GetObjectReferencesForSymbolTableEntry(NativeReadContext context, LocalHeap heap, SymbolTableEntry entry, H5LinkAccess linkAccess)
    {
        var name = heap.GetObjectName(entry.LinkNameOffset);
        var reference = new NativeNamedReference(name, entry.HeaderAddress, Context.File);

        return entry.ScratchPad switch
        {
            ObjectHeaderScratchPad objectScratch => AddScratchPad(reference, objectScratch),

            SymbolicLinkScratchPad linkScratch => await new SymbolicLink(
                name,
                heap.GetObjectName(linkScratch.LinkValueOffset),
                this,
                Context.File).GetTarget(context, linkAccess).ConfigureAwait(false),

            _ when !context.Superblock.IsUndefinedAddress(entry.HeaderAddress) => reference,

            _ => throw new Exception("Unknown object type.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NativeNamedReference AddScratchPad(NativeNamedReference reference, ObjectHeaderScratchPad scratchPad)
    {
        reference.ScratchPad = scratchPad;
        return reference;
    }

    private static async IAsyncEnumerable<SymbolTableNode> EnumerateSymbolTableNodes(
        NativeReadContext context,
        BTree1Node<BTree1GroupKey> btree1)
    {
        await foreach (var node in btree1.EnumerateNodes(context, DecodeGroupKey))
        {
            foreach (var address in node.ChildAddresses)
            {
                context.Driver.SeekRelativeToBaseAddress((long)address);
                yield return await SymbolTableNode.Decode(context).ConfigureAwait(false);
            }
        }
    }

    #endregion

    #region Callbacks

    // ASYNC PROPAGATION: BTree1Node<T>'s `compare3` parameter is now
    // `Func<T, T, ValueTask<int>>` (BTree1Node.cs, out of scope but already converted),
    // so the former `.GetAwaiter().GetResult()` bridge is no longer needed here — this
    // callback is awaited by BTree1Node<T>.LocateRecord itself.
    //
    // Deliberately not `async`, even though it returns a ValueTask to satisfy that delegate: now that
    // LocalHeap holds its data segment outright, comparing two names reads nothing and there is
    // nothing to suspend on. This runs once per b-tree comparison, so a state machine here would sit
    // on the inner loop of every by-name lookup.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<int> NodeCompare3(LocalHeap localHeap, string name, BTree1GroupKey leftKey, BTree1GroupKey rightKey)
    {
        // H5Gnode.c (H5G_node_cmp3)

        /* left side */
        var leftName = localHeap.GetObjectName(leftKey.LocalHeapByteOffset);

        if (string.CompareOrdinal(name, leftName) <= 0)
        {
            return new ValueTask<int>(-1);
        }
        else
        {
            /* right side */
            var rightName = localHeap.GetObjectName(rightKey.LocalHeapByteOffset);

            if (string.CompareOrdinal(name, rightName) > 0)
            {
                return new ValueTask<int>(1);
            }
        }

        return new ValueTask<int>(0);
    }

    // ASYNC PROPAGATION: `FoundDelegate<T, TUserData>` (BTree1Node.cs, out of scope but
    // already converted) is now `ValueTask<(bool Success, TUserData UserData)> (ulong
    // address, T leftNode)` — the `out` parameter became a tuple return because `out`
    // cannot coexist with `async` (CS1988). Callers curry `localHeap`/`name` via a
    // lambda (see TryGetReference) matching this method's remaining parameters.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<(bool Success, BTree1SymbolTableUserData UserData)> NodeFound(NativeReadContext context, LocalHeap localHeap, string name, ulong address)
    {
        // H5Gnode.c (H5G__node_found)
        uint low = 0, index = 0, high;
        int cmp = 1;

        /*
         * Load the symbol table node for exclusive access.
         */
        context.Driver.SeekRelativeToBaseAddress((long)address);
        var symbolTableNode = await SymbolTableNode.Decode(context).ConfigureAwait(false);

        /*
         * Binary search.
         */
        high = symbolTableNode.SymbolCount;

        while (low < high && cmp != 0)
        {
            index = (low + high) / 2;

            var linkNameOffset = symbolTableNode.GroupEntries[(int)index].LinkNameOffset;
            var currentName = localHeap.GetObjectName(linkNameOffset);
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

    // ASYNC PROPAGATION: `BTree1Node<T>.Decode(...)` and `SymbolTableMessage.GetBTree1` take a
    // `DecodeKeyDelegate<T>`, which `BTree1GroupKey.Decode` (BTree1Types.cs, already async) is simply
    // awaited through instead of bridged.
    //
    // CONCURRENCY: takes the operation's context instead of reading the group's file-level one, so
    // the key decode happens on the same driver as the b-tree traversal that asks for it. Callers
    // pass this method group directly - it used to be curried as `() => DecodeGroupKey(context)`,
    // which allocated a closure per navigation call and, worse, made the decoded b-tree
    // uncacheable, because the tree held the delegate and the delegate held a per-operation context.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async ValueTask<BTree1GroupKey> DecodeGroupKey(NativeReadContext context)
    {
        return await BTree1GroupKey.Decode(context).ConfigureAwait(false);
    }

    #endregion
}
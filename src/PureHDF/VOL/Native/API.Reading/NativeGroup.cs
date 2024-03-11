using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace PureHDF.VOL.Native;

/// <summary>
/// An HDF5 group.
/// </summary>
public class NativeGroup : NativeAttributableObject, IH5Group
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
        return EnumerateReferences(linkAccess)
            .Select(reference => reference.Dereference());
    }

    private bool InternalLinkExists(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return true;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? Context.File.Reference : Reference;

        for (int i = 0; i < segments.Length; i++)
        {
            if (current.Dereference() is not NativeGroup group)
                return false;

            if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                return false;

            current = reference;
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

        for (int i = 0; i < segments.Length; i++)
        {
            // TODO: Use cache to store dereferenced objects (as it is done in HsdsGroup.cs)
            if (current.Dereference() is not NativeGroup group)
                throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");

            if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                throw new Exception($"Could not find part of the path '{path}'.");

            current = reference;
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

    private bool TryGetReference(string name, H5LinkAccess linkAccess, out NativeNamedReference namedReference)
    {
        namedReference = default;

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
            var localHeap = _scratchPad.LocalHeap;

            var success = _scratchPad
                .GetBTree1(DecodeGroupKey)
                .TryFindUserData(out var userData,
                                (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                                (ulong address, BTree1GroupKey _, out BTree1SymbolTableUserData userData)
                                    => NodeFound(localHeap, name, address, out userData));

            if (success)
            {
                namedReference = GetObjectReferencesForSymbolTableEntry(localHeap, userData.SymbolTableEntry, linkAccess);
                return true;
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
                var localHeap = smessage.LocalHeap;

                var success = smessage
                    .GetBTree1(DecodeGroupKey)
                    .TryFindUserData(out var userData,
                                    (leftKey, rightKey) => NodeCompare3(localHeap, name, leftKey, rightKey),
                                    (ulong address, BTree1GroupKey _, out BTree1SymbolTableUserData userData)
                                        => NodeFound(localHeap, name, address, out userData));

                if (success)
                {
                    namedReference = GetObjectReferencesForSymbolTableEntry(localHeap, userData.SymbolTableEntry, linkAccess);
                    return true;
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
                        if (TryGetLinkMessageFromLinkInfoMessage(lmessage, name, out var linkMessage))
                        {
                            namedReference = GetObjectReference(linkMessage, linkAccess);
                            return true;
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
                            namedReference = GetObjectReference(linkMessage, linkAccess);
                            return true;
                        }
                    }
                }
                else
                {
                    throw new Exception("No link information found in object header.");
                }
            }
        }

        return false;
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
            var references = EnumerateReferences(linkAccess)
                .ToList();

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

    private IEnumerable<NativeNamedReference> EnumerateReferences(H5LinkAccess linkAccess)
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
            var localHeap = _scratchPad.LocalHeap;
            var references = this
                .EnumerateSymbolTableNodes(_scratchPad.GetBTree1(DecodeGroupKey))
                .SelectMany(node => node.GroupEntries
                .Select(entry => GetObjectReferencesForSymbolTableEntry(localHeap, entry, linkAccess)));

            foreach (var reference in references)
            {
                yield return reference;
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
                var localHeap = smessage.LocalHeap;
                var references = this
                    .EnumerateSymbolTableNodes(smessage.GetBTree1(DecodeGroupKey))
                    .SelectMany(node => node.GroupEntries
                    .Select(entry => GetObjectReferencesForSymbolTableEntry(localHeap, entry, linkAccess)));

                foreach (var reference in references)
                {
                    yield return reference;
                }
            }

            else
            {
                var linkInfoMessages = Header.GetMessages<LinkInfoMessage>();

                if (linkInfoMessages.Any())
                {
                    IEnumerable<LinkMessage> linkMessages;

                    if (linkInfoMessages.Count() != 1)
                        throw new Exception("There may be only a single link info message.");

                    var lmessage = linkInfoMessages.First();

                    /* New (1.8) indexed format (in combination with Group Info Message) 
                     * IV.A.2.c. The Link Info Message 
                     * Optional; may not be repeated. */
                    if (!Context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                        linkMessages = EnumerateLinkMessagesFromLinkInfoMessage(lmessage);

                    /* New (1.8) compact format
                     * IV.A.2.g. The Link Message 
                     * A group is storing its links compactly when the fractal heap address 
                     * in the Link Info Message is set to the "undefined address" value. */
                    else
                        linkMessages = Header.GetMessages<LinkMessage>();

                    // build links
                    foreach (var linkMessage in linkMessages)
                    {
                        yield return GetObjectReference(linkMessage, linkAccess);
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

    private IEnumerable<LinkMessage> EnumerateLinkMessagesFromLinkInfoMessage(LinkInfoMessage infoMessage)
    {
        var fractalHeap = infoMessage.FractalHeap;
        var btree2NameIndex = infoMessage.BTree2NameIndex;
        var records = btree2NameIndex
            .EnumerateRecords()
            .ToList();

        // local cache: indirectly accessed, non-filtered
        List<BTree2Record01>? record01Cache = null;

        foreach (var record in records)
        {
            using var localDriver = new H5StreamDriver(new MemoryStream(record.HeapId), leaveOpen: false);
            var heapId = FractalHeapId.Construct(Context, localDriver, fractalHeap);

            yield return heapId.Read(driver =>
            {
                var message = LinkMessage.Decode(Context);
                return message;
            }, ref record01Cache);
        }
    }

    private bool TryGetLinkMessageFromLinkInfoMessage(LinkInfoMessage linkInfoMessage,
                                                      string name,
                                                      [NotNullWhen(returnValue: true)] out LinkMessage? linkMessage)
    {
        linkMessage = null;

        var fractalHeap = linkInfoMessage.FractalHeap;
        var btree2NameIndex = linkInfoMessage.BTree2NameIndex;
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var nameHash = ChecksumUtils.JenkinsLookup3(nameBytes);
        var candidate = default(LinkMessage);

        var success = btree2NameIndex.TryFindRecord(out var record, record =>
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
                var heapId = FractalHeapId.Construct(Context, localDriver, fractalHeap);
                candidate = heapId.Read(driver => LinkMessage.Decode(Context));

                // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                return string.CompareOrdinal(name, candidate.LinkName);
            }
        });

        if (success)
        {
            if (candidate is null)
                throw new Exception("This should never happen. Just to satisfy the compiler.");

            linkMessage = candidate;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NativeNamedReference GetObjectReference(LinkMessage linkMessage, H5LinkAccess linkAccess)
    {
        return linkMessage.LinkInfo switch
        {
            HardLinkInfo hard => new NativeNamedReference(linkMessage.LinkName, hard.HeaderAddress, Context.File),
            SoftLinkInfo soft => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(linkAccess),
#if NET6_0_OR_GREATER
            ExternalLinkInfo external => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(linkAccess),
#else
            ExternalLinkInfo external => new SymbolicLink(linkMessage, this, Context.File)
                .GetTarget(linkAccess),
#endif

            _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
        };
    }

    #endregion

    #region Symbol Table

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NativeNamedReference GetObjectReferencesForSymbolTableEntry(LocalHeap heap, SymbolTableEntry entry, H5LinkAccess linkAccess)
    {
        var name = heap.GetObjectName(entry.LinkNameOffset);
        var reference = new NativeNamedReference(name, entry.HeaderAddress, Context.File);

        return entry.ScratchPad switch
        {
            ObjectHeaderScratchPad objectScratch => AddScratchPad(reference, objectScratch),

            SymbolicLinkScratchPad linkScratch => new SymbolicLink(
                name,
                heap.GetObjectName(linkScratch.LinkValueOffset),
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

    private IEnumerable<SymbolTableNode> EnumerateSymbolTableNodes(BTree1Node<BTree1GroupKey> btree1)
    {
        return btree1.EnumerateNodes().SelectMany(node =>
        {
            return node.ChildAddresses.Select(address =>
            {
                Context.Driver.Seek((long)address, SeekOrigin.Begin);
                return SymbolTableNode.Decode(Context);
            });
        });
    }

    #endregion

    #region Callbacks

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NodeCompare3(LocalHeap localHeap, string name, BTree1GroupKey leftKey, BTree1GroupKey rightKey)
    {
        // H5Gnode.c (H5G_node_cmp3)

        /* left side */
        var leftName = localHeap.GetObjectName(leftKey.LocalHeapByteOffset);

        if (string.CompareOrdinal(name, leftName) <= 0)
        {
            return -1;
        }
        else
        {
            /* right side */
            var rightName = localHeap.GetObjectName(rightKey.LocalHeapByteOffset);

            if (string.CompareOrdinal(name, rightName) > 0)
            {
                return 1;
            }
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool NodeFound(LocalHeap localHeap, string name, ulong address, out BTree1SymbolTableUserData userData)
    {
        userData = default;

        // H5Gnode.c (H5G__node_found)
        uint low = 0, index = 0, high;
        int cmp = 1;

        /*
         * Load the symbol table node for exclusive access.
         */
        Context.Driver.Seek((long)address, SeekOrigin.Begin);
        var symbolTableNode = SymbolTableNode.Decode(Context);

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
            return false;

        userData = new BTree1SymbolTableUserData(
            SymbolTableEntry: symbolTableNode.GroupEntries[(int)index]
        );

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree1GroupKey DecodeGroupKey()
    {
        return BTree1GroupKey.Decode(Context);
    }

    #endregion
}
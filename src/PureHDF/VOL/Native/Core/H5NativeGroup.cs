using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

[DebuggerDisplay("{Name}")]
internal class H5NativeGroup : H5AttributableObject, IH5NativeGroup
{
    #region Fields

    private readonly H5NativeFile? _file;
    private readonly ObjectHeaderScratchPad? _scratchPad;

    #endregion

    #region Constructors

    // Only for H5File constructor
    internal H5NativeGroup(H5Context context, NamedReference reference, ObjectHeader header)
       : base(context, reference, header)
    {
        //
    }

    internal H5NativeGroup(H5NativeFile file, H5Context context, NamedReference reference)
       : base(context, reference)
    {
        _file = file;
        _scratchPad = reference.ScratchPad;
    }

    internal H5NativeGroup(H5NativeFile file, H5Context context, NamedReference reference, ObjectHeader header)
        : base(context, reference, header)
    {
        _file = file;
    }

    #endregion

    #region Properties

    internal H5NativeFile File
    {
        get
        {
            if (_file is null)
                return (H5NativeFile)this;
                
            else
                return _file;
        }
    }

    #endregion

    #region Methods
    // TODO: properly implement async

    public bool LinkExists(string path)
    {
        return LinkExists(path, default);
    }
    
    public Task<bool> LinkExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LinkExists(path));
    }

    public bool LinkExists(string path, H5LinkAccess linkAccess)
    {
        return InternalLinkExists(path, linkAccess);
    }

    public Task<bool> LinkExistsAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LinkExists(path, linkAccess));
    }

    public IH5Object Get(string path)
    {
        return Get(path, default);
    }

    public Task<IH5Object> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get(path));
    }

    public IH5Object Get(string path, H5LinkAccess linkAccess)
    {
        return InternalGet(path, linkAccess)
            .Dereference();
    }

    public Task<IH5Object> GetAsync(string path, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get(path, linkAccess));
    }

    public IH5Object Get(H5ObjectReference reference)
    {
        return Get(reference, default);
    }

    public Task<IH5Object> GetAsync(H5ObjectReference reference, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get(reference));
    }

    public IH5Object Get(H5ObjectReference reference, H5LinkAccess linkAccess)
    {
        if (Reference.Value == reference.Value)
            return this;

        return InternalGet(reference, linkAccess)
            .Dereference();
    }

    public Task<IH5Object> GetAsync(H5ObjectReference reference, H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Get(reference, linkAccess));
    }

    public IEnumerable<IH5Object> Children()
    {
        return Children(default);
    }

    public Task<IEnumerable<IH5Object>> ChildrenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Children());
    }

    public IEnumerable<IH5Object> Children(H5LinkAccess linkAccess = default)
    {
        return EnumerateReferences(linkAccess)
            .Select(reference => reference.Dereference());
    }

    public Task<IEnumerable<IH5Object>> ChildrenAsync(H5LinkAccess linkAccess, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Children(linkAccess));
    }

    private bool InternalLinkExists(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return true;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? File.Reference : Reference;

        for (int i = 0; i < segments.Length; i++)
        {
            if (current.Dereference() is not H5NativeGroup group)
                return false;

            if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                return false;

            current = reference;
        }

        return true;
    }

    internal NamedReference InternalGet(string path, H5LinkAccess linkAccess)
    {
        if (path == "/")
            return File.Reference;

        var isRooted = path.StartsWith("/");
        var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
        var current = isRooted ? File.Reference : Reference;

        for (int i = 0; i < segments.Length; i++)
        {
            if (current.Dereference() is not H5NativeGroup group)
                throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");

            if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                throw new Exception($"Could not find part of the path '{path}'.");

            current = reference;
        }

        return current;
    }

    internal NamedReference InternalGet(H5ObjectReference reference, H5LinkAccess linkAccess)
    {
        var alreadyVisted = new HashSet<ulong>();

        if (TryGetReference(reference, alreadyVisted, linkAccess, recursionLevel: 0, out var namedReference))
            return namedReference;
        else
            throw new Exception($"Could not find object for reference with value '{reference.Value:X}'.");
    }

    private bool TryGetReference(string name, H5LinkAccess linkAccess, out NamedReference namedReference)
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

    internal bool TryGetReference(H5ObjectReference reference, HashSet<ulong> alreadyVisited, H5LinkAccess linkAccess, int recursionLevel, out NamedReference namedReference)
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
            var references = this
                .EnumerateReferences(linkAccess)
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
                    var group = childReference.Dereference() as H5NativeGroup;

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

    private IEnumerable<NamedReference> EnumerateReferences(H5LinkAccess linkAccess)
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
                var message = new LinkMessage(Context);
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
        var nameHash = ChecksumUtils.JenkinsLookup3(name);
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
                candidate = heapId.Read(driver => new LinkMessage(Context));

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
    private NamedReference GetObjectReference(LinkMessage linkMessage, H5LinkAccess linkAccess)
    {
        return linkMessage.LinkInfo switch
        {
            HardLinkInfo hard => new NamedReference(linkMessage.LinkName, hard.HeaderAddress, File),
            SoftLinkInfo soft => new SymbolicLink(linkMessage, this)
                .GetTarget(linkAccess, useAsync: default),
#if NET6_0_OR_GREATER
            ExternalLinkInfo external => new SymbolicLink(linkMessage, this)
                .GetTarget(linkAccess, useAsync: Context.Driver is H5FileHandleDriver driver && driver.IsAsync),
#else
                ExternalLinkInfo external => new SymbolicLink(linkMessage, this)
                    .GetTarget(linkAccess, useAsync: default),
#endif

            _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
        };
    }

    #endregion

    #region Symbol Table

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NamedReference GetObjectReferencesForSymbolTableEntry(LocalHeap heap, SymbolTableEntry entry, H5LinkAccess linkAccess)
    {
        var name = heap.GetObjectName(entry.LinkNameOffset);
        var reference = new NamedReference(name, entry.HeaderAddress, File);

        return entry.ScratchPad switch
        {
            ObjectHeaderScratchPad objectScratch => AddScratchPad(reference, objectScratch),
            SymbolicLinkScratchPad linkScratch => new SymbolicLink(name, heap.GetObjectName(linkScratch.LinkValueOffset), this).GetTarget(linkAccess, useAsync: default),
            _ when !Context.Superblock.IsUndefinedAddress(entry.HeaderAddress) => reference,
            _ => throw new Exception("Unknown object type.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NamedReference AddScratchPad(NamedReference reference, ObjectHeaderScratchPad scratchPad)
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
                return new SymbolTableNode(Context);
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
        var symbolTableNode = new SymbolTableNode(Context);

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

        userData.SymbolTableEntry = symbolTableNode.GroupEntries[(int)index];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree1GroupKey DecodeGroupKey()
    {
        return new BTree1GroupKey(Context);
    }

    #endregion
}
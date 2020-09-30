using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5Group : H5AttributableObject
    {
        #region Fields

        private H5File? _file;
        private ObjectHeaderScratchPad? _scratchPad;

        #endregion

        #region Constructors

        // Only for H5File constructor
        internal H5Group(H5Context context, H5NamedReference reference, ObjectHeader header)
           : base(context, reference, header)
        {
            //
        }

        internal H5Group(H5File file, H5Context context, H5NamedReference reference)
           : base(context, reference)
        {
            _file = file;
            _scratchPad = reference.ScratchPad;
        }

        internal H5Group(H5File file, H5Context context, H5NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            _file = file;
        }

        #endregion

        #region Properties

        public IEnumerable<H5Object> Children
            => this.GetChildren(new H5LinkAccessPropertyList());

        internal H5File File
        {
            get
            {
                if (_file == null)
                    return (H5File)this;
                else
                    return _file;
            }
        }

        #endregion

        #region Public

        public bool Exists(string path)
        {
            return this.Exists(path, new H5LinkAccessPropertyList());
        }

        public bool Exists(string path, H5LinkAccessPropertyList linkAccess)
        {
            return this.InternalExists(path, linkAccess);
        }

        public H5Object Get(string path)
        {
            return this.Get(path, new H5LinkAccessPropertyList());
        }

        public H5Object Get(string path, H5LinkAccessPropertyList linkAccess)
        {
            return this
                .InternalGet(path, linkAccess)
                .Dereference();
        }

        public H5Group Group(string path)
        {
            return this.Group(path, new H5LinkAccessPropertyList());
        }

        public H5Group Group(string path, H5LinkAccessPropertyList linkAccess)
        {
            var link = this.Get(path, linkAccess);
            var castedLink = link as H5Group;

            if (castedLink == null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Group)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        public H5Dataset Dataset(string path)
        {
            return this.Dataset(path, new H5LinkAccessPropertyList());
        }

        public H5Dataset Dataset(string path, H5LinkAccessPropertyList linkAccess)
        {
            var link = this.Get(path, linkAccess);
            var castedLink = link as H5Dataset;

            if (castedLink == null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5Dataset)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        public H5CommitedDatatype CommitedDatatype(string path)
        {
            return this.CommitedDatatype(path, new H5LinkAccessPropertyList());
        }

        public H5CommitedDatatype CommitedDatatype(string path, H5LinkAccessPropertyList linkAccess)
        {
            var link = this.Get(path, linkAccess);
            var castedLink = link as H5CommitedDatatype;

            if (castedLink == null)
                throw new Exception($"The requested link exists but cannot be casted to {nameof(H5CommitedDatatype)} because it is of type {link.GetType().Name}.");

            return castedLink;
        }

        public IEnumerable<H5Object> GetChildren(H5LinkAccessPropertyList linkAccess)
        {
            return this
                .EnumerateReferences(linkAccess)
                .Select(reference => reference.Dereference());
        }

        #region Private

        private bool InternalExists(string path, H5LinkAccessPropertyList linkAccess)
        {
            if (path == "/")
                return true;

            var isRooted = path.StartsWith('/');
            var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
            var current = isRooted ? this.File.Reference : this.Reference;

            for (int i = 0; i < segments.Length; i++)
            {
                var group = current.Dereference() as H5Group;

                if (group == null)
                    return false;

                if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                    return false;

                current = reference;
            }

            return true;
        }

        internal H5NamedReference InternalGet(string path, H5LinkAccessPropertyList linkAccess)
        {
            if (path == "/")
                return this.Reference;

            var isRooted = path.StartsWith('/');
            var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
            var current = isRooted ? this.File.Reference : this.Reference;

            for (int i = 0; i < segments.Length; i++)
            {
                var group = current.Dereference() as H5Group;

                if (group == null)
                    throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");

                if (!group.TryGetReference(segments[i], linkAccess, out var reference))
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = reference;
            }

            return current;
        }

        private bool TryGetReference(string name, H5LinkAccessPropertyList linkAccess, out H5NamedReference reference)
        {
            reference = default;

            // scratch pad info seems to be unused in HDF5 reference implementation (search for stab.btree_addr)

            /* cached data */
            /*
            if (_scratchPad != null)
            {
                var localHeap = _scratchPad.LocalHeap;

                this.Context.Reader.Seek((long)_scratchPad.BTree1Address, SeekOrigin.Begin);
                var tree = new BTree1Node<BTree1GroupKey>(this.Context.Reader, this.Context.Superblock, this.DecodeGroupKey);
                var b = tree.EnumerateNodes().ToList();

                this.Context.Reader.Seek((long)_scratchPad.NameHeapAddress, SeekOrigin.Begin);
                var heap = new LocalHeap(this.Context.Reader, this.Context.Superblock);
                var c = heap.GetObjectName(0);


                var success = _scratchPad
                    .GetBTree1(this.DecodeGroupKey)
                    .TryFindUserData(out var userData,
                                    (leftKey, rightKey) => this.NodeCompare3(localHeap, name, leftKey, rightKey),
                                    (ulong address, out BTree1SymbolTableUserData userData) => this.NodeFound(localHeap, name, address, out userData));

                if (success)
                {
                    @object = this.InstantiateObjectForSymbolTableEntry(localHeap, userData.SymbolTableEntry);
                    return true;
                }
            }
            else
            */
            {
                var symbolTableHeaderMessages = this.Header.GetMessages<SymbolTableMessage>();

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
                        .GetBTree1(this.DecodeGroupKey)
                        .TryFindUserData(out var userData,
                                        (leftKey, rightKey) => this.NodeCompare3(localHeap, name, leftKey, rightKey),
                                        (ulong address, out BTree1SymbolTableUserData userData) => this.NodeFound(localHeap, name, address, out userData));

                    if (success)
                    {
                        reference = this.GetObjectReferencesForSymbolTableEntry(localHeap, userData.SymbolTableEntry, linkAccess);
                        return true;
                    }
                }
                else
                {
                    var linkInfoMessages = this.Header.GetMessages<LinkInfoMessage>();

                    if (linkInfoMessages.Any())
                    {
                        if (linkInfoMessages.Count() != 1)
                            throw new Exception("There may be only a single link info message.");

                        var lmessage = linkInfoMessages.First();

                        /* New (1.8) indexed format (in combination with Group Info Message) 
                         * IV.A.2.c. The Link Info Message 
                         * Optional; may not be repeated. */
                        if (!this.Context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                        {
                            if (this.TryGetLinkMessageFromLinkInfoMessage(lmessage, name, out var linkMessage))
                            {
                                reference = this.GetObjectReference(linkMessage, linkAccess);
                                return true;
                            }
                        }
                        /* New (1.8) compact format
                         * IV.A.2.g. The Link Message 
                         * A group is storing its links compactly when the fractal heap address 
                         * in the Link Info Message is set to the "undefined address" value. */
                        else
                        {
                            var linkMessage = this.Header
                                .GetMessages<LinkMessage>()
                                .FirstOrDefault(message => message.LinkName == name);

                            if (linkMessage != null)
                            {
                                reference = this.GetObjectReference(linkMessage, linkAccess);
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

        private IEnumerable<H5NamedReference> EnumerateReferences(H5LinkAccessPropertyList linkAccess)
        {
            // https://support.hdfgroup.org/HDF5/doc/RM/RM_H5G.html 
            // section "Group implementations in HDF5"

            // scratch pad info seems to be unused in HDF5 reference implementation (search for stab.btree_addr)

            /* cached data */
            /*
            if (_scratchPad != null)
            {
                var localHeap = _scratchPad.LocalHeap;
                var links = this
                    .EnumerateSymbolTableNodes(_scratchPad.GetBTree1(this.DecodeGroupKey))
                    .SelectMany(node => node.GroupEntries
                    .Select(entry => this.InstantiateLinkForSymbolTableEntry(localHeap, entry)));

                foreach (var link in links)
                {
                    yield return link;  
                }
            }
            else
            */
            {
                var symbolTableHeaderMessages = this.Header.GetMessages<SymbolTableMessage>();

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
                        .EnumerateSymbolTableNodes(smessage.GetBTree1(this.DecodeGroupKey))
                        .SelectMany(node => node.GroupEntries
                        .Select(entry => this.GetObjectReferencesForSymbolTableEntry(localHeap, entry, linkAccess)));

                    foreach (var reference in references)
                    {
                        yield return reference;
                    }
                }
                else
                {
                    var linkInfoMessages = this.Header.GetMessages<LinkInfoMessage>();

                    if (linkInfoMessages.Any())
                    {
                        IEnumerable<LinkMessage> linkMessages;

                        if (linkInfoMessages.Count() != 1)
                            throw new Exception("There may be only a single link info message.");

                        var lmessage = linkInfoMessages.First();

                        /* New (1.8) indexed format (in combination with Group Info Message) 
                         * IV.A.2.c. The Link Info Message 
                         * Optional; may not be repeated. */
                        if (!this.Context.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                            linkMessages = this.EnumerateLinkMessagesFromLinkInfoMessage(lmessage);

                        /* New (1.8) compact format
                         * IV.A.2.g. The Link Message 
                         * A group is storing its links compactly when the fractal heap address 
                         * in the Link Info Message is set to the "undefined address" value. */
                        else
                            linkMessages = this.Header.GetMessages<LinkMessage>();

                        // build links
                        foreach (var linkMessage in linkMessages)
                        {
                            yield return this.GetObjectReference(linkMessage, linkAccess);
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
                using var localReader = new H5BinaryReader(new MemoryStream(record.HeapId));
                var heapId = FractalHeapId.Construct(this.Context, localReader, fractalHeap);

                yield return heapId.Read(reader =>
                {
                    var message = new LinkMessage(reader, this.Context.Superblock);
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
            var nameHash = H5Checksum.JenkinsLookup3(name);
            var candidate = default(LinkMessage);

            var success = btree2NameIndex.TryFindRecord(out var record, record =>
            {
#warning Better to implement comparison code in record (here: BTree2Record05) itself?

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
#warning duplicate2
                    using var localReader = new H5BinaryReader(new MemoryStream(record.HeapId));
                    var heapId = FractalHeapId.Construct(this.Context, localReader, fractalHeap);
                    candidate = heapId.Read(reader => new LinkMessage(reader, this.Context.Superblock));

                    // https://stackoverflow.com/questions/35257814/consistent-string-sorting-between-c-sharp-and-c
                    // https://stackoverflow.com/questions/492799/difference-between-invariantculture-and-ordinal-string-comparison
                    return string.CompareOrdinal(name, candidate.LinkName);
                }
            });

            if (success)
            {
                if (candidate == null)
                    throw new Exception("This should never happen. Just to satisfy the compiler.");

                linkMessage = candidate;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private H5NamedReference GetObjectReference(LinkMessage linkMessage, H5LinkAccessPropertyList linkAccess)
        {
            return linkMessage.LinkInfo switch
            {
                HardLinkInfo hard => new H5NamedReference(this.File, linkMessage.LinkName, hard.HeaderAddress),
                SoftLinkInfo soft => new H5SymbolicLink(linkMessage, this).GetTarget(linkAccess),
                ExternalLinkInfo external => new H5SymbolicLink(linkMessage, this).GetTarget(linkAccess),
                _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
            };
        }

        #endregion

        #region Symbol Table

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private H5NamedReference GetObjectReferencesForSymbolTableEntry(LocalHeap heap, SymbolTableEntry entry, H5LinkAccessPropertyList linkAccess)
        {
            var name = heap.GetObjectName(entry.LinkNameOffset);
            var reference = new H5NamedReference(this.File, name, entry.HeaderAddress);

            return entry.ScratchPad switch
            {
                ObjectHeaderScratchPad objectScratch => this.AddScratchPad(reference, objectScratch),
                SymbolicLinkScratchPad linkScratch => new H5SymbolicLink(name, heap.GetObjectName(linkScratch.LinkValueOffset), this).GetTarget(linkAccess),
                _ when !this.Context.Superblock.IsUndefinedAddress(entry.HeaderAddress) => reference,
                _ => throw new Exception("Unknown object type.")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private H5NamedReference AddScratchPad(H5NamedReference reference, ObjectHeaderScratchPad scratchPad)
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
                    this.Context.Reader.Seek((long)address, SeekOrigin.Begin);
                    return new SymbolTableNode(this.Context.Reader, this.Context.Superblock);
                });
            });
        }

        #endregion

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NodeCompare3(LocalHeap localHeap, string name, BTree1GroupKey leftKey, BTree1GroupKey rightKey)
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

            // H5Gnode.c (H5G_node_found)
            uint low = 0, index = 0, high;
            int cmp = 1;

            /*
             * Load the symbol table node for exclusive access.
             */
            this.Context.Reader.Seek((long)address, SeekOrigin.Begin);
            var symbolTableNode = new SymbolTableNode(this.Context.Reader, this.Context.Superblock);

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
            return new BTree1GroupKey(this.Context.Reader, this.Context.Superblock);
        }

        #endregion
    }
}

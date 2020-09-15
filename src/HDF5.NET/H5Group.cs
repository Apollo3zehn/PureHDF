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
    public class H5Group : H5AttributableLink
    {
        #region Fields

        private ObjectHeaderScratchPad? _scratchPad;

        #endregion

        #region Constructors

        // only for root group
        internal H5Group(ObjectHeader objectHeader)
           : base(objectHeader)
        {
            //   
        }

        internal H5Group(H5File file, string name, ObjectHeader objectHeader)
            : base(file, name, objectHeader)
        {
            //
        }

        internal H5Group(H5File file, string name, ulong objectHeaderAddress, ObjectHeaderScratchPad? scratchPad)
            : base(file, name, objectHeaderAddress)
        {
            _scratchPad = scratchPad;
        }

        #endregion

        #region Properties

        public IEnumerable<H5Link> Children => this.EnumerateLinks();

        #endregion

        #region Methods

        public bool LinkExists(string path)
        {
            if (!path.StartsWith('/'))
                return false;

            if (path == "/")
                return true;

            var segments = path.Split('/');
            var current = (H5Link)this;

            foreach (var pathSegment in segments.Skip(1))
            {
                var group = current as H5Group;

                if (group == null)
                    return false;

                if (!group.TryGetLink(pathSegment, out var link))
                    return false;

                current = link;
            }

            return true;
        }

        public T Get<T>(string path) where T : H5Link
        {
            return (T)this.Get(path);
        }

        public H5Link Get(string path)
        {
            return this.InternalGet(path, resolveSymboliclink: true);
        }

        public H5SymbolicLink GetSymbolicLink(string path)
        {
            return (H5SymbolicLink)this.InternalGet(path, resolveSymboliclink: false);
        }

        private H5Link InternalGet(string path, bool resolveSymboliclink = true)
        {
            if (path == "/")
                return this;

            var isRooted = path.StartsWith('/');
            var segments = isRooted ? path.Split('/').Skip(1).ToArray() : path.Split('/');
            var current = (H5Link)(isRooted ? this.File : this);

            H5SymbolicLink? symbolicLink;

            for (int i = 0; i < segments.Length; i++)
            {
                // either it is a group
                var group = current as H5Group;

                if (group == null)
                {
                    // or it is a symbolic link to a group
                    symbolicLink = current as H5SymbolicLink;

                    if (symbolicLink == null)
                    {
                        throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");
                    }
                    else
                    {
                        group = symbolicLink.Target as H5Group;

                        if (group == null)
                            throw new Exception($"Path segment '{segments[i - 1]}' is not a group.");
                    }
                }

                if (!group.TryGetLink(segments[i], out var link))
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = link;
            }

            symbolicLink = current as H5SymbolicLink;

            if (symbolicLink != null && resolveSymboliclink)
                current = symbolicLink.Target;

            return current;
        }

        private bool TryGetLink(string name, [NotNullWhen(returnValue: true)] out H5Link? link)
        {
            link = null;

            /* cached data */
            if (_scratchPad != null)
            {
                var localHeap = _scratchPad.LocalHeap;

                var success = _scratchPad.BTree1.TryFindUserData(out var userData,
                        (leftKey, rightKey) => this.NodeCompare3(localHeap, name, leftKey, rightKey),
                        (ulong address, out BTree1SymbolTableUserData userData) => this.NodeFound(localHeap, name, address, out userData));

                if (success)
                {
                    link = this.InstantiateLinkForSymbolTableEntry(localHeap, userData.SymbolTableEntry);
                    return true;
                }
            }
            else
            {
                var symbolTableHeaderMessages = this.ObjectHeader.GetMessages<SymbolTableMessage>();

                if (symbolTableHeaderMessages.Any())
                {
                    /* Original approach.
                     * IV.A.2.r.: The Symbol Table Message
                     * Required for "old style" groups; may not be repeated. */

                    if (symbolTableHeaderMessages.Count() != 1)
                        throw new Exception("There may be only a single symbol table header message.");

                    var smessage = symbolTableHeaderMessages.First();
                    var localHeap = smessage.LocalHeap;

                    var success = smessage.BTree1.TryFindUserData(out var userData,
                        (leftKey, rightKey) => this.NodeCompare3(localHeap, name, leftKey, rightKey),
                        (ulong address, out BTree1SymbolTableUserData userData) => this.NodeFound(localHeap, name, address, out userData));

                    if (success)
                    {
                        link = this.InstantiateLinkForSymbolTableEntry(localHeap, userData.SymbolTableEntry);
                        return true;
                    }
                }
                else
                {
                    var linkInfoMessages = this.ObjectHeader.GetMessages<LinkInfoMessage>();

                    if (linkInfoMessages.Any())
                    {
                        if (linkInfoMessages.Count() != 1)
                            throw new Exception("There may be only a single link info message.");

                        var lmessage = linkInfoMessages.First();

                        /* New (1.8) indexed format (in combination with Group Info Message) 
                         * IV.A.2.c. The Link Info Message 
                         * Optional; may not be repeated. */
                        if (!this.File.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                        {
                            if (this.TryGetLinkMessageFromLinkInfoMessage(lmessage, name, out var linkMessage))
                            {
                                link = this.InstantiateLink(linkMessage);
                                return true;
                            }
                        }
                        /* New (1.8) compact format
                         * IV.A.2.g. The Link Message 
                         * A group is storing its links compactly when the fractal heap address 
                         * in the Link Info Message is set to the "undefined address" value. */
                        else
                        {
                            var linkMessage = this.ObjectHeader
                                .GetMessages<LinkMessage>()
                                .FirstOrDefault(message => message.LinkName == name);

                            if (linkMessage != null)
                            {
                                link = this.InstantiateLink(linkMessage);
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

        private IEnumerable<H5Link> EnumerateLinks()
        {
            // https://support.hdfgroup.org/HDF5/doc/RM/RM_H5G.html 
            // section "Group implementations in HDF5"

            /* cached data */
            if (_scratchPad != null)
            {
                var localHeap = _scratchPad.LocalHeap;
                var links = this
                    .EnumerateSymbolTableNodes(_scratchPad.BTree1)
                    .SelectMany(node => node.GroupEntries
                    .Select(entry => this.InstantiateLinkForSymbolTableEntry(localHeap, entry)));

                foreach (var link in links)
                {
                    yield return link;
                }
            }
            else
            {
                var symbolTableHeaderMessages = this.ObjectHeader.GetMessages<SymbolTableMessage>();

                if (symbolTableHeaderMessages.Any())
                {
                    /* Original approach.
                     * IV.A.2.r.: The Symbol Table Message
                     * Required for "old style" groups; may not be repeated. */

                    if (symbolTableHeaderMessages.Count() != 1)
                        throw new Exception("There may be only a single symbol table header message.");

                    var smessage = symbolTableHeaderMessages.First();
                    var localHeap = smessage.LocalHeap;
                    var links = this
                        .EnumerateSymbolTableNodes(smessage.BTree1)
                        .SelectMany(node => node.GroupEntries
                        .Select(entry => this.InstantiateLinkForSymbolTableEntry(localHeap, entry)));

                    foreach (var link in links)
                    {
                        yield return link;
                    }
                }
                else
                {
                    var linkInfoMessages = this.ObjectHeader.GetMessages<LinkInfoMessage>();

                    if (linkInfoMessages.Any())
                    {
                        IEnumerable<LinkMessage> linkMessages;

                        if (linkInfoMessages.Count() != 1)
                            throw new Exception("There may be only a single link info message.");

                        var lmessage = linkInfoMessages.First();

                        /* New (1.8) indexed format (in combination with Group Info Message) 
                         * IV.A.2.c. The Link Info Message 
                         * Optional; may not be repeated. */
                        if (!this.File.Superblock.IsUndefinedAddress(lmessage.BTree2NameIndexAddress))
                            linkMessages = this.EnumerateLinkMessagesFromLinkInfoMessage(lmessage);

                        /* New (1.8) compact format
                         * IV.A.2.g. The Link Message 
                         * A group is storing its links compactly when the fractal heap address 
                         * in the Link Info Message is set to the "undefined address" value. */
                        else
                            linkMessages = this.ObjectHeader.GetMessages<LinkMessage>();

                        // build links
                        foreach (var linkMessage in linkMessages)
                        {
                            yield return this.InstantiateLink(linkMessage);
                        }
                    }
                    else
                    {
                        throw new Exception("No link information found in object header.");
                    }
                }
            }
        }

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
                var heapId = FractalHeapId.Construct(this.File.Reader, this.File.Superblock, localReader, fractalHeap);

                yield return heapId.Read(reader =>
                {
                    var message = new LinkMessage(reader, this.File.Superblock);
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
                    var heapId = FractalHeapId.Construct(this.File.Reader, this.File.Superblock, localReader, fractalHeap);
                    candidate = heapId.Read(reader => new LinkMessage(reader, this.File.Superblock));

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
        private H5Link InstantiateLink(LinkMessage linkMessage)
        {
            return linkMessage.LinkInfo switch
            {
                HardLinkInfo hard => this.InstantiateUncachedLink(linkMessage.LinkName, hard.ObjectHeader),
                SoftLinkInfo soft => new H5SymbolicLink(linkMessage, this),
                ExternalLinkInfo external => new H5SymbolicLink(linkMessage, this),
                _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private H5Link InstantiateUncachedLink(string name, ObjectHeader? objectHeader)
        {
            if (objectHeader != null)
            {
                return objectHeader.ObjectType switch
                {
                    H5ObjectType.Group => new H5Group(this.File, name, objectHeader),
                    H5ObjectType.Dataset => new H5Dataset(this.File, name, objectHeader),
                    H5ObjectType.CommitedDataType => new H5CommitedDataType(name, objectHeader),
                    _ => throw new Exception("Unknown object type.")
                };
            }
            else
            {
                throw new Exception("Unknown object type.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private H5Link InstantiateLinkForSymbolTableEntry(LocalHeap heap, SymbolTableEntry entry)
        {
            var name = heap.GetObjectName(entry.LinkNameOffset);

            return entry.ScratchPad switch
            {
                ObjectHeaderScratchPad objectScratch => new H5Group(this.File, name, entry.ObjectHeaderAddress, objectScratch),
                SymbolicLinkScratchPad linkScratch => new H5SymbolicLink(name, heap.GetObjectName(linkScratch.LinkValueOffset), this),
                _ => this.InstantiateUncachedLink(name, entry.ObjectHeader)
            };
        }

        private IEnumerable<SymbolTableNode> EnumerateSymbolTableNodes(BTree1Node<BTree1GroupKey> btree1)
        {
            var nodeLevel = 0U;

            return btree1.GetTree()[nodeLevel].SelectMany(node =>
            {
                return node.ChildAddresses.Select(address =>
                {
                    this.File.Reader.Seek((long)address, SeekOrigin.Begin);
                    return new SymbolTableNode(this.File.Reader, this.File.Superblock);
                });
            });
        }

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
            this.File.Reader.Seek((long)address, SeekOrigin.Begin);
            var symbolTableNode = new SymbolTableNode(this.File.Reader, this.File.Superblock);

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

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

        public IEnumerable<H5Link> Children
        {
            get
            {
                foreach (var link in this.EnumerateLinks())
                {
                    yield return link;
                }
            }
        }

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

                var links = group.EnumerateLinks();
                var link = links.FirstOrDefault(namedObjects => namedObjects.Name == pathSegment);

                if (link == null)
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

#warning Improve this to traverse to specific link instead of enumeration
                var link = group
                    .EnumerateLinks()
                    .FirstOrDefault(link => link.Name == segments[i]);

                if (link == null)
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = link;
            }

            symbolicLink = current as H5SymbolicLink;

            if (symbolicLink != null)
                current = symbolicLink.Target;

            return current;
        }

        private IEnumerable<H5Link> InstantiateLinks(LocalHeap heap, List<SymbolTableEntry> entries)
        {
            foreach (var entry in entries)
            {
                var name = heap.GetObjectName(entry.LinkNameOffset);

                yield return entry.ScratchPad switch
                {
                    ObjectHeaderScratchPad objectScratch    => new H5Group(this.File, name, entry.ObjectHeaderAddress, objectScratch),
                    SymbolicLinkScratchPad linkScratch      => new H5SymbolicLink(name, heap.GetObjectName(linkScratch.LinkValueOffset), this),
                    _                                       => this.InstantiateUncachedLink(name, entry.ObjectHeader)
                };
            }
        }

        private IEnumerable<H5Link> EnumerateLinks()
        {
            // https://support.hdfgroup.org/HDF5/doc/RM/RM_H5G.html 
            // section "Group implementations in HDF5"

            /* original approach */

            // use cached data
            if (_scratchPad != null)
            {
                var localHeap = _scratchPad.LocalHeap;
                var links = _scratchPad.BTree1
                    .GetSymbolTableNodes()
                    .SelectMany(node => this.InstantiateLinks(localHeap, node.GroupEntries));

                foreach (var link in links)
                {
                    yield return link;
                }
            }
            // look up symbol table header messages
            else
            {
                var symbolTableHeaderMessages = this.ObjectHeader
                .GetMessages<SymbolTableMessage>();

                if (symbolTableHeaderMessages.Any())
                {
                    foreach (var message in symbolTableHeaderMessages)
                    {
                        var localHeap = message.LocalHeap;
                        var links = message.BTree1
                            .GetSymbolTableNodes()
                            .SelectMany(node => this.InstantiateLinks(localHeap, node.GroupEntries));

                        foreach (var link in links)
                        {
                            yield return link;
                        }
                    }
                }
            }

            /* new (1.8) indexed format (in combination with Group Info Message) */
            var linkInfoMessages = this.ObjectHeader
                .GetMessages<LinkInfoMessage>()
                .Where(message => !this.File.Superblock.IsUndefinedAddress(message.BTree2NameIndexAddress));

            foreach (var linkInfoMessage in linkInfoMessages)
            {
                throw new Exception("Link info messages are not yet supported. Please provide a sample file to the package author.");
            }

            // new (1.8) compact format
            // IV.A.2.g. The Link Message 
            // A group is storing its links compactly when the fractal heap address in the Link Info Message is set to the “undefined address” value.
            var linkMessages = this.ObjectHeader
                .GetMessages<LinkMessage>();

            foreach (var linkMessage in linkMessages)
            {
                yield return linkMessage.LinkInfo switch
                {
                    HardLinkInfo hard => this.InstantiateUncachedLink(linkMessage.LinkName, hard.ObjectHeader),
                    SoftLinkInfo soft => new H5SymbolicLink(linkMessage, this),
                    ExternalLinkInfo external => new H5SymbolicLink(linkMessage, this),
                    _ => throw new Exception($"Unknown link type '{linkMessage.LinkType}'.")
                };
            }
        }

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

        #endregion
    }
}

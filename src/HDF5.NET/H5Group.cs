using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5Group : H5AttributableLink
    {
        #region Fields

        public ObjectHeaderScratchPad? _scratchPad;

        #endregion

        #region Constructors

        internal H5Group(BinaryReader reader, Superblock superblock, string name, ulong objectHeaderAddress, ObjectHeaderScratchPad? scratchPad)
            : base(reader, superblock, name, objectHeaderAddress)
        {
            _scratchPad = scratchPad;
        }

        internal H5Group(BinaryReader reader, Superblock superblock, string name, ObjectHeader objectHeader)
            : base(reader, superblock, name, objectHeader)
        {
            //
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
            if (!path.StartsWith('/'))
                throw new Exception("A path must start with a forward slash, e.g. '/a/b/c'.");

            if (path == "/")
                return (T)(H5Link)this;

            var segments = path.Split('/');
            var current = (H5Link)this;

            foreach (var pathSegment in segments.Skip(1))
            {
                var group = current as H5Group;

                if (group == null)
                    throw new Exception($"Path segment '{pathSegment}' is not a group.");

                var links = group.EnumerateLinks();
                var link = links.FirstOrDefault(link => link.Name == pathSegment);

                if (link == null)
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = link;
            }

            return (T)current;
        }

        private IEnumerable<H5Link> InstantiateLinks(LocalHeap heap, List<SymbolTableEntry> entries)
        {
            foreach (var entry in entries)
            {
                var name = heap.GetObjectName(entry.LinkNameOffset);

                yield return entry.ScratchPad switch
                {
                    ObjectHeaderScratchPad objectScratch    => new H5Group(this.Reader, this.Superblock, name, entry.ObjectHeaderAddress, objectScratch),
                    SymbolicLinkScratchPad linkScratch      => new H5SymbolicLink(this.Superblock, name, entry),
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
                .GetMessages<LinkInfoMessage>();

            if (linkInfoMessages.Any())
            {
                //namedObjects = namedObjects.AddRange(linkInfoMessages
                //    .Where(message => !_superblock.IsUndefinedAddress(message.FractalHeapAddress))
                //    .Select(message =>
                //{
                //    throw new NotImplementedException();
                //    //return message.BTree1.GetSymbolTableNodes()
                //    //    .SelectMany(node => this.CreateNamedObjects(message.LocalHeap, node.GroupEntries));
                //}));
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
                    SoftLinkInfo soft => new H5SymbolicLink(this.Superblock, linkMessage),
                    ExternalLinkInfo external => new H5SymbolicLink(this.Superblock, linkMessage),
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
                    H5ObjectType.Group => new H5Group(this.Reader, this.Superblock, name, objectHeader),
                    H5ObjectType.Dataset => new H5Dataset(this.Reader, this.Superblock, name, objectHeader),
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

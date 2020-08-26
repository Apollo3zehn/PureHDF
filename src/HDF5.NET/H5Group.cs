using System;
using System.Collections.Generic;
using System.Linq;

namespace HDF5.NET
{
    public class H5Group : H5Link
    {
        #region Fields

        private List<H5Link> _children;

        #endregion

        #region Constructors

        internal H5Group(NamedObject namedObject, Superblock superblock) 
            : base(namedObject, superblock)
        {
            //
        }

        #endregion

        #region Properties

        public List<H5Link> Children
        {
            get
            {
                if (_children == null)
                    _children = this.GetChildren();

                return _children;
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
            var current = this.ObjectHeader;

            foreach (var pathSegment in segments.Skip(1))
            {
                if (current.ObjectType != H5ObjectType.Group)
                    return false;

                var namedObjects = this.EnumerateNamedObjects(current);
                var namedObject = namedObjects.FirstOrDefault(namedObjects => namedObjects.Name == pathSegment);

                if (namedObject.Equals(default(NamedObject)))
                    return false;

                current = namedObject.Header;
            }

            return true;
        }

        public T Get<T>(string path) where T : H5Link
        {
            if (!path.StartsWith('/'))
                throw new Exception("A path must start with a forward slash, e.g. '/a/b/c'.");

            if (path == "/")
                return (T)(object)this;

            var segments = path.Split('/');
            var current = new NamedObject(this.Name, this.ObjectHeader);

            foreach (var pathSegment in segments.Skip(1))
            {
                if (current.Header.ObjectType != H5ObjectType.Group)
                    throw new Exception($"Path segment '{pathSegment}' is not a group.");

                var namedObjects = this.EnumerateNamedObjects(current.Header);
                var namedObject = namedObjects.FirstOrDefault(namedObjects => namedObjects.Name == pathSegment);

                if (namedObject.Equals(default(NamedObject)))
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = namedObject;
            }

            return current.Header.ObjectType switch
            {
                H5ObjectType.Group              => (T)(object)new H5Group(current, this.Superblock),
                H5ObjectType.Dataset            => (T)(object)new H5Dataset(current, this.Superblock),
                H5ObjectType.CommitedDataType   => (T)(object)new H5CommitedDataType(current, this.Superblock),
                _                               => throw new Exception("Unknown object type.")
            };
        }

        private List<H5Link> GetChildren()
        {
            var namedObjects = this.EnumerateNamedObjects(this.ObjectHeader);

            return namedObjects.Select(namedObject =>
            {
                return namedObject.Header.ObjectType switch
                {
                    H5ObjectType.Group              => (H5Link)new H5Group(namedObject, this.Superblock),
                    H5ObjectType.Dataset            => (H5Link)new H5Dataset(namedObject, this.Superblock),
                    H5ObjectType.CommitedDataType   => (H5Link)new H5CommitedDataType(namedObject, this.Superblock),
                    _                               => throw new Exception("Unknown object type.")
                };
            }).ToList();
        }

        private IEnumerable<NamedObject> CreateNamedObjects(LocalHeap heap, List<SymbolTableEntry> entries)
        {
            return entries
                .Select(entry =>
                {
                    var name = heap.GetObjectName(entry.LinkNameOffset);
                    return new NamedObject(name, entry.ObjectHeader);
                });
        }

        private IEnumerable<NamedObject> EnumerateNamedObjects(ObjectHeader header)
        {
#warning new NamedObject(..., objectHeader) loads the object header. Better use Lazy<T>?

            // https://support.hdfgroup.org/HDF5/doc/RM/RM_H5G.html 
            // section "Group implementations in HDF5"
            var namedObjects = (IEnumerable<NamedObject>)new List<NamedObject>();

            // original approach
            var symbolTableHeaderMessages = header
                .GetMessages<SymbolTableMessage>();

            if (symbolTableHeaderMessages.Any())
            {
                namedObjects = namedObjects.Concat(symbolTableHeaderMessages
                    .SelectMany(message =>
                {
                    return message.BTree1
                        .GetSymbolTableNodes()
                        .SelectMany(node => this.CreateNamedObjects(message.LocalHeap, node.GroupEntries));
                }));
            }

            // new (1.8) indexed format (in combination with Group Info Message)
            var linkInfoMessages = header
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
            var linkMessages = header
                .GetMessages<LinkMessage>();

            if (linkMessages.Any())
            {
                namedObjects = namedObjects.Concat(linkMessages
                    .Where(message => message.LinkType == LinkType.Hard)
                    .Select(message =>
                {
                    var linkInfo = message.LinkInfo as HardLinkInfo;
                    return new NamedObject(message.LinkName, linkInfo.ObjectHeader);
                }));
            }

            return namedObjects;
        }

        #endregion
    }
}

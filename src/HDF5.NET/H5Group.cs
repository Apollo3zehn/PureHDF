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

        internal H5Group(string name, ObjectHeader header, Superblock superblock) : base(name, header, superblock)
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

                var symbolTableHeaderMessages = current.GetHeaderMessages<SymbolTableMessage>();

                var namedEntries = symbolTableHeaderMessages.Count switch
                {
#warning Validate all three paths against spec
                    0 => this.GetNamedEntriesFromLinkMessages(),
                    1 => this.GetNamedEntriesFromSymbolTable(symbolTableHeaderMessages[0]),
                    _ => throw new FormatException("The number of symbol table messages must be 0 or 1.")
                };

                var namedEntry = namedEntries.FirstOrDefault(namedEntries => namedEntries.Name == pathSegment);

                if (namedEntry == default)
                    return false;

                current = namedEntry.Entry.ObjectHeader;
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
            var current = this.ObjectHeader;

            foreach (var pathSegment in segments.Skip(1))
            {
                if (current.ObjectType != H5ObjectType.Group)
                    throw new Exception($"Path segment '{pathSegment}'. is not a group.");

                var symbolTableHeaderMessages = current.GetHeaderMessages<SymbolTableMessage>();

                var namedEntries = symbolTableHeaderMessages.Count switch
                {
#warning Validate all three paths against spec
                    0 => this.GetNamedEntriesFromLinkMessages(),
                    1 => this.GetNamedEntriesFromSymbolTable(symbolTableHeaderMessages[0]),
                    _ => throw new FormatException("The number of symbol table messages must be 0 or 1.")
                };

                var namedEntry = namedEntries.FirstOrDefault(namedEntries => namedEntries.Name == pathSegment);

                if (namedEntry == default)
                    throw new Exception($"Could not find part of the path '{path}'.");

                current = namedEntry.Entry.ObjectHeader;
            }

            return current.ObjectType switch
            {
                H5ObjectType.Group              => (T)(object)new H5Group(segments.Last(), current, this.Superblock),
                H5ObjectType.Dataset            => (T)(object)new H5Dataset(segments.Last(), current, this.Superblock),
                H5ObjectType.CommitedDataType   => (T)(object)new H5CommitedDataType(segments.Last(), current, this.Superblock),
                _                               => throw new Exception("Unknown object type.")
            };
        }

        private List<H5Link> GetChildren()
        {
            var symbolTableHeaderMessages = this.ObjectHeader.GetHeaderMessages<SymbolTableMessage>();
            
            var namedEntries = symbolTableHeaderMessages.Count switch
            {
#warning Validate all three paths against spec
                0 => this.GetNamedEntriesFromLinkMessages(),
                1 => this.GetNamedEntriesFromSymbolTable(symbolTableHeaderMessages[0]),
                _ => throw new FormatException("The number of symbol table messages must be 0 or 1.")
            };

            return namedEntries.Select(namedEntry =>
            {
                if (namedEntry.Entry.CacheType >= CacheType.SymbolicLink)
                    throw new NotImplementedException("Symbolic links are not supported yet.");

                return namedEntry.Entry.ObjectHeader.ObjectType switch
                {
                    H5ObjectType.Group              => (H5Link)new H5Group(namedEntry.Name, namedEntry.Entry.ObjectHeader, this.Superblock),
                    H5ObjectType.Dataset            => (H5Link)new H5Dataset(namedEntry.Name, namedEntry.Entry.ObjectHeader, this.Superblock),
                    H5ObjectType.CommitedDataType   => (H5Link)new H5CommitedDataType(namedEntry.Name, namedEntry.Entry.ObjectHeader, this.Superblock),
                    _                               => throw new Exception("Unknown object type.")
                };
            }).ToList();
        }

        private List<(string Name, SymbolTableEntry Entry)> GetNamedEntriesFromLinkMessages()
        {
            throw new NotImplementedException();
        }

        private List<(string Name, SymbolTableEntry Entry)> GetNamedEntriesFromSymbolTable(SymbolTableMessage message)
        {
            var btree = message.BTree1;
            var heap = message.LocalHeap;

            var entries = btree.GetSymbolTableNodes()
                .SelectMany(node => this.GetNamedEntries(heap, node.GroupEntries))
                .ToList();

            return entries;
        }

        public List<(string Name, SymbolTableEntry Entry)> GetNamedEntries(LocalHeap heap, List<SymbolTableEntry> entries)
        {
            return entries
                .Select(entry =>
                {
                    var name = heap.GetObjectName(entry.LinkNameOffset);
                    return (name, entry);
                })
                .ToList();
        }

        #endregion
    }
}

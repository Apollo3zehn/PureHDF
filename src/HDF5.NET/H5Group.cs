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

        internal H5Group(string name, ObjectHeader header) : base(name, header)
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

        private List<H5Link> GetChildren()
        {
            var symbolTableHeaderMessages = this.ObjectHeader.HeaderMessages
                .Where(message => message.Type == HeaderMessageType.SymbolTable)
                .ToList();

            var symbolTableEntries = symbolTableHeaderMessages.Count switch
            {
#warning Validate all three paths against spec
                0 => this.GetLinksFromLinkMessages(),
                1 => this.GetLinksFromSymbolTable((SymbolTableMessage)symbolTableHeaderMessages[0].Data),
                _ => throw new FormatException("The number of symbol table messages must be 0 or 1.")
            };

            return symbolTableEntries.Select(entry =>
            {
                if (entry.CacheType >= CacheType.SymbolicLink)
                    throw new NotImplementedException("Symbolic links are not supported yet.");

                var isDataset = entry.ObjectHeader.HeaderMessages.Any(message => message.Type == HeaderMessageType.Dataspace);

                if (isDataset)
                    return (H5Link)new H5Dataset(entry.Name, entry.ObjectHeader);
                else
                    return (H5Link)new H5Group(entry.Name, entry.ObjectHeader);
            }).ToList();
        }

        private List<SymbolTableEntry> GetLinksFromLinkMessages()
        {
            throw new NotImplementedException();
        }

        private List<SymbolTableEntry> GetLinksFromSymbolTable(SymbolTableMessage message)
        {
            var btree = message.BTree1;
            var heap = message.LocalHeap;

            var entries = btree.GetSymbolTableNodes().SelectMany(node =>
            {
                node.AssignNames(heap);
                return node.GroupEntries;
            }).ToList();

            return entries;
        }

        #endregion
    }
}

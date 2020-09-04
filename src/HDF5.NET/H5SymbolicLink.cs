using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Target = '{Target}'")]
    public class H5SymbolicLink : H5Link
    {
        #region Fields

        private SymbolTableEntry? _symbolTableEntry;

        private LinkMessage? _linkMessage;

        #endregion

        #region Constructors

        internal H5SymbolicLink(Superblock superblock, string name, SymbolTableEntry symbolTableEntry) : base(name)
        {
            _symbolTableEntry = symbolTableEntry;
            this.Superblock = superblock;
        }

        internal H5SymbolicLink(Superblock superblock, LinkMessage linkMessage) : base(linkMessage.LinkName)
        {
            _linkMessage = linkMessage;
            this.Superblock = superblock;
        }

        #endregion

        #region Properties

        public Superblock Superblock { get; }

        #endregion

        
    }
}

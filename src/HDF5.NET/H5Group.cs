namespace HDF5.NET
{
    public class H5Group
    {
        #region Fields

        private SymbolTableEntry _symbolTableEntry;

        #endregion

        #region Constructors

        internal H5Group(SymbolTableEntry symbolTableEntry)
        {
            _symbolTableEntry = symbolTableEntry;
        }

        #endregion

        #region Methods

        //public IEnumerable<H5Group> GetChildren()
        //{
        //    return _symbolTableEntry.ObjectHeader.HeaderMessages.Select(headerMessage =>
        //    {
        //        switch (headerMessage.Type)
        //        {
        //            case HeaderMessageType.SymbolTable:

        //                break;

        //            default:
        //                throw new NotSupportedException($"The header message type '{headerMessage.Type}' is not supported yet.");
        //        }
        //    });
        //}

        #endregion
    }
}

namespace HDF5.NET
{
    internal enum MessageType : ushort
    {
        NIL = 0x000,
        Dataspace = 0x0001,
        LinkInfo = 0x0002,
        Datatype = 0x0003,
        OldFillValue = 0x004,
        FillValue = 0x0005,
        Link = 0x0006,
        ExternalDataFiles = 0x0007,
        DataLayout = 0x0008,
        Bogus = 0x0009,
        GroupInfo = 0x000A,
        FilterPipeline = 0x000B,
        Attribute = 0x000C,
        ObjectComment = 0x000D,
        OldObjectModificationTime = 0x000E,
        SharedMessageTable = 0x000F,
        ObjectHeaderContinuation = 0x0010,
        SymbolTable = 0x0011,
        ObjectModification = 0x0012,
        BTreeKValues = 0x0013,
        DriverInfo = 0x0014,
        AttributeInfo = 0x0015,
        ObjectReferenceCount = 0x0016
    }
}

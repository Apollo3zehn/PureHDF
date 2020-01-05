namespace HDF5.NET
{
    public enum HeaderMessageType : ushort
    {
        NIL = 0x000,
        Dataspace = 0x0001,
        LinkInfo = 0x0002,
        DataType = 0x0003,
        OldFillValueMessage = 0x004,
        FillValueMessage = 0x0005,
        LinkMessage = 0x0006,
        ExternalDataFilesMessage = 0x0007,
        DataLayoutMessage = 0x0008,
        BogusMessage = 0x0009,
        GroupInfoMessage = 0x000A,
        FilterPipelineMessage = 0x000B,
        AttributeMessage = 0x000C,
        ObjectCommentMessage = 0x000D,
        OldObjectModificationTimeMessage = 0x000E,
        SharedMessageTableMessage = 0x000F,
        ObjectHeaderContinuationMessage = 0x0010,
        SymbolTableMessage = 0x0011,
        ObjectModificationMessage = 0x0012,
        BTreeKValuesMessage = 0x0013,
        DriverInfoMessage = 0x0014,
        AttributeInfoMessage = 0x0015,
        ObjectReferenceCountMessage = 0x0016
    }
}

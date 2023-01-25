namespace PureHDF
{
    [Flags]
    internal enum AttributeMessageFlags : byte
    {
        SharedDatatype = 1,
        SharedDataspace = 2
    }
}

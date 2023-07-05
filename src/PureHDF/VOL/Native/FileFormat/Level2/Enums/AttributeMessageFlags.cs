namespace PureHDF.VOL.Native;

[Flags]
internal enum AttributeMessageFlags : byte
{
    None = 0,
    SharedDatatype = 1,
    SharedDataspace = 2
}
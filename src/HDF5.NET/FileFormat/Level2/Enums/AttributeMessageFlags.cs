using System;

namespace HDF5.NET
{
    [Flags]
    internal enum AttributeMessageFlags : byte
    {
        SharedDatatype = 1,
        SharedDataspace = 2
    }
}

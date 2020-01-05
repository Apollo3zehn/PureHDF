using System;

namespace HDF5.NET
{
    [Flags]
    public enum AttributeMessageFlags : byte
    {
        SharedDatatype = 1,
        SharedDataspace = 2
    }
}

using System;

namespace HDF5.NET
{
    [Flags]
    public enum MessageTypeFlags : ushort
    {
        Dataspace = 0,
        DataType = 1,
        FillValue = 2,
        FilterPipeline = 3,
        Attribute = 4
    }
}

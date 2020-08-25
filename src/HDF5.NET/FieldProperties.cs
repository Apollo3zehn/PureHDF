using System;

namespace HDF5.NET
{
    struct FieldProperties
    {
        public IntPtr Offset { get; set; }
        public Type Type { get; set; }
    }
}

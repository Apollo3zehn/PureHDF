using System;
using System.Reflection;

namespace HDF5.NET
{
    struct FieldProperties
    {
        public FieldInfo FieldInfo { get; set; }
        public IntPtr Offset { get; set; }
    }
}

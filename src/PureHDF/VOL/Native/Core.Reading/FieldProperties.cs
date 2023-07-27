using System.Reflection;

namespace PureHDF;

internal struct FieldProperties
{
    public FieldInfo FieldInfo { get; set; }
    public IntPtr Offset { get; set; }
}
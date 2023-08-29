using System.Reflection;

namespace PureHDF.VOL.Native;

internal struct FieldProperties
{
    public FieldInfo FieldInfo { get; set; }
    public IntPtr Offset { get; set; }
}
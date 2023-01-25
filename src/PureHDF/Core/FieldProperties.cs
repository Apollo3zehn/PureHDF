using System.Reflection;

namespace PureHDF
{
    struct FieldProperties
    {
        public FieldInfo FieldInfo { get; set; }
        public IntPtr Offset { get; set; }
    }
}

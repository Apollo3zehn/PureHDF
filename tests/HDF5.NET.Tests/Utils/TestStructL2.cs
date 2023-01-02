using System.Runtime.InteropServices;

namespace HDF5.NET.Tests
{
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public unsafe struct TestStructL2
    {
        [FieldOffset(0)]
        public byte ByteValue;

        [FieldOffset(1)]
        public ushort UShortValue;

        [FieldOffset(3)]
        public TestEnum EnumValue;

        [FieldOffset(5)]
        public fixed float FloatArray[3];
    }
}

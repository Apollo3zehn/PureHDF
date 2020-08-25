using System.Runtime.InteropServices;

namespace HDF5.NET.Tests
{
    [StructLayout(LayoutKind.Explicit, Size = 3)]
    public struct TestStructL2
    {
        [FieldOffset(0)]
        public byte ByteValue;

        [FieldOffset(1)]
        public ushort UShortValue;
    }
}

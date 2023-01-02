using System.Runtime.InteropServices;

namespace HDF5.NET.Tests
{
    public struct TestStructStringAndArrayL2
    {
        public float FloatValue;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue1;
    }
}

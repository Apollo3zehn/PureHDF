using System.Runtime.InteropServices;

namespace PureHDF.Tests
{
    public struct TestStructStringAndArrayL1
    {
        public float FloatValue;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue1;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue2;

        public TestStructStringAndArrayL2 L2Struct;
    }
}

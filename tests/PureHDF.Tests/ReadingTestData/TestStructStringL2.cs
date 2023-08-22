using System.Runtime.InteropServices;

namespace PureHDF.Tests
{
    public struct TestStructStringAndArrayL2
    {
        public float FloatValue;

        [MarshalAs(UnmanagedType.LPStr)]
        public string StringValue1;
    }
}

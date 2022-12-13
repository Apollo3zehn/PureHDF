namespace HDF5.NET.Tests
{
    public class TestData
    {
        private static TestStructL1 _nn_a = new TestStructL1()
        {
            ByteValue = 1,
            UShortValue = 2,
            UIntValue = 3,
            ULongValue = 4,
            L2Struct1 = new TestStructL2()
            {
                ByteValue = 99,
                UShortValue = 65535,
                EnumValue = TestEnum.b,
            },
            SByteValue = 5,
            ShortValue = -6,
            IntValue = 7,
            LongValue = -8,
            L2Struct2 = new TestStructL2()
            {
                ByteValue = 9,
                EnumValue = TestEnum.b,
            }
        };

        private static TestStructL1 _nn_b = new TestStructL1()
        {
            ByteValue = 2,
            UShortValue = 4,
            UIntValue = 6,
            ULongValue = 8,
            L2Struct1 = new TestStructL2()
            {
                ByteValue = 99,
                UShortValue = 65535,
                EnumValue = TestEnum.a,
            },
            SByteValue = -10,
            ShortValue = 12,
            IntValue = 14,
            LongValue = -16,
            L2Struct2 = new TestStructL2()
            {
                ByteValue = 18,
                UShortValue = 20,
                EnumValue = TestEnum.b,
            }
        };

        private static TestStructString _string_a = new TestStructString()
        {
            FloatValue = (float)1.299e9,
            StringValue1 = "Hello",
            StringValue2 = "World",
            ByteValue = 123,
            ShortValueWithCustomName = -15521,
            L2Struct = new TestStructL2()
            {
                ByteValue = 15,
                UShortValue = 20,
                EnumValue = TestEnum.a,
            }
        };

        private static TestStructString _string_b = new TestStructString()
        {
            FloatValue = (float)2.299e-9,
            StringValue1 = "Hello!",
            StringValue2 = "World!",
            ByteValue = 0,
            ShortValueWithCustomName = 15521,
            L2Struct = new TestStructL2()
            {
                ByteValue = 18,
                UShortValue = 21,
                EnumValue = TestEnum.b,
            }
        };

        static TestData()
        {
            TestData.EnumData = new TestEnum[] { TestEnum.a, TestEnum.b, TestEnum.c, TestEnum.c, TestEnum.c, TestEnum.a, 
                                                 TestEnum.b, TestEnum.b, TestEnum.b, TestEnum.c, TestEnum.c, (TestEnum)99 };

            TestData.BitfieldData = new TestBitfield[] { TestBitfield.a | TestBitfield.b, TestBitfield.b, TestBitfield.c, TestBitfield.c, TestBitfield.c, TestBitfield.a,
                                                         TestBitfield.b, TestBitfield.b, TestBitfield.b, TestBitfield.e | TestBitfield.f | TestBitfield.d, TestBitfield.c, (TestBitfield)99 };

            TestData.ArrayData = new int[2, 3, 4, 5];

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        for (int l = 0; l < 5; l++)
                        {
                            TestData.ArrayData[i, j, k, l] = i * j * j * k * l;
                        }
                    }
                }
            }

            TestData.NumericalData = new List<object[]>
            {
                new object[] { "D1", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D2", new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D3", new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D4", new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D5", new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D6", new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D7", new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D8", new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D9", new float[] { 0, 1, 2, 3, 4, 5, 6, (float)-7.99, 8, 9, 10, 11 } },
                new object[] {"D10", new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11 } },
                new object[] {"D11", TestData.EnumData },
            };

            TestData.NonNullableStructData = new TestStructL1[] { _nn_a, _nn_b, _nn_a, _nn_a, _nn_b, _nn_b, _nn_b, _nn_b, _nn_a, _nn_a, _nn_b, _nn_a };
            TestData.StringStructData = new TestStructString[] { _string_a, _string_b, _string_a, _string_a, _string_b, _string_b, _string_b, _string_b, _string_a, _string_a, _string_b, _string_a };
            TestData.TinyData = new byte[] { 99 };
            TestData.SmallData = Enumerable.Range(0, 100).ToArray();
            TestData.MediumData = Enumerable.Range(0, 10_000).ToArray();
            TestData.HugeData = Enumerable.Range(0, 10_000_000).ToArray();
            TestData.HyperslabData = Enumerable.Range(0, 2*3*6).ToArray();
        }

        public static TestEnum[] EnumData { get; }

        public static TestBitfield[] BitfieldData { get; }

        public static int[,,,] ArrayData { get; }

        public static IList<object[]> NumericalData { get; }

        public static TestStructL1[] NonNullableStructData { get; }

        public static TestStructString[] StringStructData { get; }

        public static byte[] TinyData { get; }

        public static int[] SmallData { get; }

        public static int[] MediumData { get; }

        public static int[] HugeData { get; }

        public static int[] HyperslabData { get; }
    }
}
namespace PureHDF.Tests
{
    public class ReadingTestData
    {
        private static TestStructL1 _nn_a = new()
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

        private static TestStructL1 _nn_b = new()
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

        private static TestStructStringAndArray _string_a = new()
        {
            FloatValue = (float)1.299e9,
            StringValue1 = "Hello",
            StringValue2 = "World",
            ByteValue = 123,
            ShortValueWithCustomName = -15521,
            FloatArray = new float[] { 1.1f, 2.2f, 3.3f },
            L2Struct = new TestStructL2()
            {
                ByteValue = 15,
                UShortValue = 20,
                EnumValue = TestEnum.a
            }
        };

        private static TestStructStringAndArray _string_b = new()
        {
            FloatValue = (float)2.299e-9,
            StringValue1 = "Hello!",
            StringValue2 = "World!",
            ByteValue = 0,
            ShortValueWithCustomName = 15521,
            FloatArray = new float[] { 2.2f, 3.3f, 4.4f },
            L2Struct = new TestStructL2()
            {
                ByteValue = 18,
                UShortValue = 21,
                EnumValue = TestEnum.b,
            }
        };

        static ReadingTestData()
        {
            EnumData = new TestEnum[] { TestEnum.a, TestEnum.b, TestEnum.c, TestEnum.c, TestEnum.c, TestEnum.a,
                                                 TestEnum.b, TestEnum.b, TestEnum.b, TestEnum.c, TestEnum.c, (TestEnum)99 };

            BitfieldData = new TestBitfield[] { TestBitfield.a | TestBitfield.b, TestBitfield.b, TestBitfield.c, TestBitfield.c, TestBitfield.c, TestBitfield.a,
                                                         TestBitfield.b, TestBitfield.b, TestBitfield.b, TestBitfield.e | TestBitfield.f | TestBitfield.d, TestBitfield.c, (TestBitfield)99 };

            ArrayDataValue = new int[2, 3, 4, 5];

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        for (int l = 0; l < 5; l++)
                        {
                            ArrayDataValue[i, j, k, l] = i * j * j * k * l;
                        }
                    }
                }
            }

            ArrayDataVariableLengthString = new string[2, 3, 4, 5];

            var counter = 0;

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        for (int l = 0; l < 5; l++)
                        {
                            ArrayDataVariableLengthString[i, j, k, l] = counter.ToString();
                            counter++;
                        }
                    }
                }
            }

            NumericalReadData = new List<object[]>
            {
                new object[] { "D1", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D2", new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D3", new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D4", new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
                new object[] { "D5", new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D6", new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D7", new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D8", new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 } },
                new object[] { "D9", new float[] { 0, 1, 2, 3, 4, 5, 6, -7.99f, 8, 9, 10, 11 } },
                new object[] {"D10", new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11 } },
                new object[] {"D11", EnumData },
            };

            NonNullableStructData = new TestStructL1[] { _nn_a, _nn_b, _nn_a, _nn_a, _nn_b, _nn_b, _nn_b, _nn_b, _nn_a, _nn_a, _nn_b, _nn_a };
            NullableStructData = new TestStructStringAndArray[] { _string_a, _string_b, _string_a, _string_a, _string_b, _string_b, _string_b, _string_b, _string_a, _string_a, _string_b, _string_a };
            
            TinyData = new byte[] { 99 };
            SmallData = Enumerable.Range(0, 100).ToArray();
            MediumData = Enumerable.Range(0, 10_000).ToArray();
            HugeData = Enumerable.Range(0, 10_000_000).ToArray();
            HyperslabData = Enumerable.Range(0, 2 * 3 * 6).ToArray();
        }

        public static TestEnum[] EnumData { get; }

        public static TestBitfield[] BitfieldData { get; }

        public static int[,,,] ArrayDataValue { get; }
        public static string[,,,] ArrayDataVariableLengthString { get; }

        public static IList<object[]> NumericalReadData { get; }

        public static TestStructL1[] NonNullableStructData { get; }

        public static TestStructStringAndArray[] NullableStructData { get; }

        public static byte[] TinyData { get; }

        public static int[] SmallData { get; }

        public static int[] MediumData { get; }

        public static int[] HugeData { get; }

        public static int[] HyperslabData { get; }
    }
}
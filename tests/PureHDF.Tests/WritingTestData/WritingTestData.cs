namespace PureHDF.Tests;

public static class WritingTestData
{
    public static IList<object[]> Common { get; } = new List<object[]>()
    {
        /* string */
        new object[] { "ß Abc" },

        new object[] { new Dictionary<string, int> {
            ["A"] = 1, ["B"] = -2, ["C"] = 3
        }},

        new object[] { new Dictionary<string, int[,]> {
            ["A"] = new int[,]
            {
                { 0, 1, 2 },
                { 3, 4, 5 },
                { 6, 7, 8 }
            }
        }},

        /* array */
        new object[] { new int[] { 1, -2, 3 } },

        new object[] { new bool[] { true, false, true } },

        new object[] { new string?[] { "A", "ßAB", null, "C BA" } },

        new object[] {
            new int[]?[]
            {
                new int[] { 1, -2, 3 },
                null,
                new int[] { 2, -4, 6 },
            }
        },

        new object[] { new Dictionary<string, int>[] {
            new Dictionary<string, int> {
                ["A"] = 1, ["B"] = -2, ["C"] = 3
            },
            new Dictionary<string, int> {
                ["D"] = 4, ["E"] = -5
            }
        }},

        /* Memory<T> */
        new object[] { new int[] { 1, -2, 3 }.AsMemory() },

        new object[] { new bool[] { true, false, true }.AsMemory() },

        new object[] { new string[] { "A", "ßAB", "C BA" }.AsMemory() },

        /* generic IEnumerable */
        new object[] { new List<int> { 1, -2, 3 } },

        new object[] { new List<bool> { true, false, true } },

        new object[] { new List<string> { "A", "ßAB", "C BA" } },

        /* tuple (reference type) */
        new object[] { Tuple.Create(1, -2L, 3.3) },

        /* reference type */
        new object[] {
            new DataspaceMessage(
                Rank: 1,
                Flags: DataspaceMessageFlags.DimensionMaxSizes | DataspaceMessageFlags.PermuationIndices,
                Type: DataspaceType.Simple,
                Dimensions: new ulong[] { 10, 20, 30 },
                MaxDimensions: new ulong[] { 20, 40, 60 },
                PermutationIndices: null)
            {
                Version = 1
            }
        },

        new object[] { new WritingTestRecordClass(X: 1, Y: 99.38 ) },

        /* bool */
        new object[] { false },
        new object[] { true },

        /* enumeration */
        new object[] { FileAccess.Read },

        /* tuple (value type) */
        // 
        // I was only able to extract the tuple names via reflection using the TupleElementNames attribute using the 
        // return type parameter info (MethodInfo.ReturnParameter.CustomAttributes). I have no idea how to get the
        // names using an instance of the tuple. One reason could be that there is only one definition of ValueTuple
        // in the framework. 
        //
        // In general, this topic seems to be quite complicated: 
        // https://github.com/dotnet/csharplang/discussions/1906#discussioncomment-103932
        new object[] { (A: 1, B: -2L, C: 3.3) },

        /* unsigned fixed-point */
        new object[] { 2U },

        /* signed fixed-point */
        new object[] { -2 },

#if NET5_0_OR_GREATER
        /* 16 bit floating-point */
        new object[] { (Half)99.38f },
#endif

        /* 32 bit floating-point */
        new object[] { 99.38f },

        /* 64 bit floating-point */
        new object[] { 99.38 },

        /* 128 bit floating-point */
        // decimal is not an IEEE 754 data type: https://forum.hdfgroup.org/t/h5dump-displays-wrong-value-for-ieee-754-quadruple-precision-value/11330
        // new object[] { 99.38m },

        /* complex value type */
        new object[] { new WritingTestStruct() { x = 1, y = 99.38 } },

        new object[] { new WritingTestRecordStruct(X: 1, Y: 99.38 ) }
    };

    public static IList<object[]> Common_FixedLengthString { get; } = new List<object[]>()
    {
        new object[] { "ABCDEF" },

        new object[] { new string[] { "ABCDEF", "ÄÜÖß@!", "1234567", "1234", "" } },

        new object[] { new WritingTestStringRecordClass(X: 1, Y: "ABCDEFG" ) },

        new object[] { new WritingTestStringStruct() { x = 1, y = "ABCDEFG" } },
    };

    public static IList<object[]> Common_FixedLengthStringMapper { get; } = new List<object[]>()
    {
        new object[] { new WritingTestStringRecordClass(X: 1, Y: "ABCDEFG" ) },

        new object[] { new WritingTestStringStruct() { x = 1, y = "ABCDEFG" } },
    };

    public static IList<object> Numerical { get; }


#if NET7_0_OR_GREATER
    public static IList<object> Numerical_Int128 { get; }
#endif

    static WritingTestData()
    {
        Numerical = new List<object>
        {
            new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 },
            new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 },
            new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 },
            new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 },
            new float[] { 0, 1, 2, 3, 4, 5, 6, -7.99f, 8, 9, 10, 11 },
            new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11 },
            ReadingTestData.EnumData,
        };

#if NET7_0_OR_GREATER
        Numerical_Int128 = new List<object>
        {
            new UInt128[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            new Int128[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 },
        };
#endif
    }
}
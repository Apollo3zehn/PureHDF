namespace PureHDF.Tests;

public static class WritingTestData
{
    public static IList<object[]> AttributeTestData { get; } = new List<object[]>()
    {
        /* dictionary */
        new object[] { new Dictionary<string, object>() {
            ["A"] = 1, ["B"] = "-2", ["C"] = 3
        }},

        new object[] { new Dictionary<string, int> {
            ["A"] = 1, ["B"] = -2, ["C"] = 3
        }},

        /* array */
        new object[] { new int[] { 1, -2, 3 } },

        new object[] { new bool[] { true, false, true } },

        // new object[] { new string[] { "A", "ßAB", "C BA" } },

        // new object[] { new Dictionary<string, int>[] {
        //     new Dictionary<string, int> { 
        //         ["A"] = 1, ["B"] = -2, ["C"] = 3 
        //     },
        //     new Dictionary<string, int> { 
        //         ["D"] = 4, ["E"] = -5
        //     }
        // }},

        /* Memory<T> */
        new object[] { new int[] { 1, -2, 3 }.AsMemory() },

        new object[] { new bool[] { true, false, true }.AsMemory() },

        // new object[] { new string[] { "A", "ßAB", "C BA" }.AsMemory() },

        /* generic IEnumerable */
        new object[] { new List<int> { 1, -2, 3 } },

        new object[] { new List<bool> { true, false, true } },

        // new object[] { new List<string> { "A", "ßAB", "C BA" } },

        // /* string */
        new object[] { "ß Abc" },

        /* tuple (reference type) */
        new object[] { Tuple.Create(1, -2L, 3.3) },

        /* reference type */
        // new object[] { 
        //     new DataspaceMessage(
        //         Rank: 1,
        //         Flags: DataspaceMessageFlags.DimensionMaxSizes,
        //         Type: DataspaceType.Simple,
        //         DimensionSizes: new ulong[] { 10, 20, 30 },
        //         DimensionMaxSizes: new ulong[] { 10, 20, 30 },
        //         PermutationIndices: default)
        //     {
        //         Version = 1
        //     }
        // },

        new object[] { new WritingTestRecordClass(X: 1, Y: 99.38 ) },

        // /* bool */
        new object[] { false },
        new object[] { true },

        /* enumeration */
        new object[] { FileAccess.Read },

        // /* tuple (value type) */
        new object[] { (A: 1, B: -2L, C: 3.3) },

        // /* unsigned fixed-point */
        new object[] { 2U },

        // /* signed fixed-point */
        new object[] { -2 },

        /* 32 bit floating-point */
        new object[] { 99.38f },

        // /* 64 bit floating-point */
        new object[] { 99.38 },

        // /* complex value type */
        new object[] { new WritingTestStruct() { x = 1, y = 99.38 } },

        new object[] { new WritingTestRecordStruct(X: 1, Y: 99.38 ) }
    };
}
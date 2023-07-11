using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    public static IList<object[]> AttributeValidTestData { get; } = new List<object[]>()
    {
        // TODO: check if T[,] should also be supported
        // TODO: what about T[,], T[][] and T[,,x], T[][][x]?

        // /* dictionary */
        new object[] { new Dictionary<string, object>() {
            ["A"] = 1, ["B"] = "-2", ["C"] = 3
        }},

        // new object[] { new Dictionary<string, int> { 
        //     ["A"] = 1, ["B"] = -2, ["C"] = 3 
        // }},

        // /* array */
        // new object[] { new int[] { 1, -2, 3 } },

        // new object[] { new Dictionary<string, int>[] {
        //     new Dictionary<string, int> { 
        //         ["A"] = 1, ["B"] = -2, ["C"] = 3 
        //     },
        //     new Dictionary<string, int> { 
        //         ["D"] = 4, ["E"] = -5
        //     }
        // }},

        // /* generic IEnumerable */
        // new object[] { new List<int> { 1, -2, 3 } },

        // /* string */
        // new object[] { "Abc" },

        // /* tuple (reference type) */
        // new object[] { Tuple.Create(1, -2, 3.3) },

        // /* random reference type */
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

        // /* bool */
        // new object[] { true },

        // /* enumeration */
        // new object[] { FileAccess.Read },

        // // /* tuple (value type) */
        // new object[] { (A: 1, B: -2L, C: 3.3) },

        // // /* random value type */
        // new object[] { new Point(x: 1, y: 99) }
    };

    public static IList<object[]> AttributeInvalidTestData { get; } = new List<object[]>()
    {
        new object[] { new Dictionary<object, object>() },
    };

    [Fact]
    public void CanWriteAttribute_Numerical()
    {
        // Arrange
        var file = new Experimental.H5File();

        foreach (var data in TestData.NumericalWriteData)
        {
            var type = data.GetType();
            file.Attributes[type.Name] = data;
        }

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("TestFiles/expected.attributetests_numerical.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(AttributeValidTestData))]
    public void CanWriteAttribute_NonNumerical(object data)
    {
        // Arrange
        var file = new Experimental.H5File();
        file.Attributes[data.GetType().Name] = data;

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("TestFiles/expected.attributetests_enumerable.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }

    // [Fact]
    // public void CanWriteAttribute_NonNullableStruct()
    // {
    //     // Arrange
    //     var data = TestData.NonNullableStructData;
    //     var file = new Experimental.H5File();

    //     file.Attributes[typeof(TestStructL1).Name] = data;

    //     var filePath = Path.GetTempFileName();

    //     // Act
    //     file.Save(filePath);

    //     // Assert
    //     var expected = File
    //         .ReadAllText("TestFiles/expected.attributetests_nonnullablestruct.dump")
    //         .Replace("<file-path>", filePath);

    //     var actual = TestUtils.DumpH5File(filePath);

    //     Assert.Equal(expected, actual);
    // }

    [Theory]
    [MemberData(nameof(AttributeInvalidTestData))]
    public void ThrowsForInvalidDataType(object data)
    {
        // Arrange
        var file = new Experimental.H5File();
        file.Attributes["data"] = data;

        var filePath = Path.GetTempFileName();

        // Act
        void action() => file.Save(filePath);

        // Assert
        Assert.Throws<Exception>(action);
    }
}
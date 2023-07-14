using System.Collections;
using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    private struct Point
    {
        public int x;
        public double y;
    };

    public static IList<object[]> AttributeTestData { get; } = new List<object[]>()
    {
        // TODO: check if T[,] should also be supported
        // TODO: what about T[,], T[][] and T[,,x], T[][][x]?

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

        // new object[] { new Dictionary<string, int>[] {
        //     new Dictionary<string, int> { 
        //         ["A"] = 1, ["B"] = -2, ["C"] = 3 
        //     },
        //     new Dictionary<string, int> { 
        //         ["D"] = 4, ["E"] = -5
        //     }
        // }},

        /* generic IEnumerable */
        new object[] { new List<int> { 1, -2, 3 } },

        // /* string */
        // new object[] { "Abc" },

        /* tuple (reference type) */
        new object[] { Tuple.Create(1, -2L, 3.3) },

        /* random reference type */
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
        new object[] { new Point() { x = 1, y = 99.38 } }
    };

    [Theory]
    [MemberData(nameof(AttributeTestData))]
    public void CanWriteAttribute(object data)
    {
        // Arrange
        var type = data.GetType();
        var file = new Experimental.H5File();
        file.Attributes[data.GetType().Name] = data;

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var actual = TestUtils.DumpH5File(filePath);

        var suffix = type switch
        {
            Type when type == typeof(bool) => $"_{data}",
            Type when typeof(IDictionary).IsAssignableFrom(type) => $"_{type.GenericTypeArguments[0].Name}_{type.GenericTypeArguments[1].Name}",
            Type when typeof(IEnumerable).IsAssignableFrom(type) && !type.IsArray => $"_{type.GenericTypeArguments[0].Name}",
            _ => default
        };

        var expected = File
            .ReadAllText($"DumpFiles/attribute_{type.Name}{suffix}.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanWriteAttribute_Anonymous()
    {
        // Arrange
        var file = new Experimental.H5File();

        var data = new
        {
            Numerical = 1,
            Boolean = true,
            Enum = FileAccess.Read,
            Anonymous = new {
                A = 1,
                B = 9.81
            }
        };

        var type = data.GetType();

        file.Attributes[type.Name] = data;

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText($"DumpFiles/attribute_{type.Name}.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanWriteAttribute_Large_Array()
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
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText("DumpFiles/attribute_large_array.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }
}
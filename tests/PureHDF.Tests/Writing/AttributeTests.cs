using System.Collections;
using PureHDF.Experimental;
using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    public static IList<object[]> AttributeValidTestData { get; } = new List<object[]>()
    {
        /* array */
        new object[] { new List<int> { 1, -2, 3 } },
        new object[] { new int[] { 1, -2, 3 } },

        /* dictionary */
        new object[] { new Dictionary<string, int> { ["A"] = 1, ["B"] = -2, ["C"] = 3 } },

        /* tuple (reference type) */
        new object[] { Tuple.Create(1) },


        new object[] { (A: 1, B: -2, C: 3.3) },


    };

    public static IList<object[]> AttributeInvalidTestData { get; } = new List<object[]>()
    {
        new object[] { new Dictionary<object, object>() },
        new object[] { (IEnumerable)new List<object>() }
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

    [Fact]
    public void CanWriteAttribute_NonNullableStruct()
    {
        // Arrange
        var data = TestData.NonNullableStructData;
        var file = new Experimental.H5File();

        file.Attributes[typeof(TestStructL1).Name] = data;

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var expected = File
            .ReadAllText("TestFiles/expected.attributetests_nonnullablestruct.dump")
            .Replace("<file-path>", filePath);

        var actual = TestUtils.DumpH5File(filePath);

        Assert.Equal(expected, actual);
    }

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
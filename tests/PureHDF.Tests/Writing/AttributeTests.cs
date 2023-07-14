using System.Collections;
using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    public static IList<object[]> AttributeTestData { get; } = WritingTestData.AttributeTestData;

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
            Type when type.IsGenericType && typeof(Memory<>).Equals(type.GetGenericTypeDefinition()) => $"_{type.GenericTypeArguments[0].Name}",
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

        foreach (var data in ReadingTestData.NumericalWriteData)
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
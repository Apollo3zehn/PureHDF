using System.Collections;
using System.Reflection;
using Xunit;

namespace PureHDF.Tests.Writing;

public class AttributeTests
{
    public static IList<object[]> CommonData { get; } = WritingTestData.Common;
    public static IList<object[]> CommonData_FixedLengthString { get; } = WritingTestData.Common_FixedLengthString;
    public static IList<object[]> CommonData_FixedLengthStringMapper { get; } = WritingTestData.Common_FixedLengthStringMapper;

    [Theory]
    [MemberData(nameof(CommonData))]
    public void CanWriteCommon(object data)
    {
        // Arrange
        var type = data.GetType();
        var file = new H5File();
        file.Attributes[type.Name] = data;

        var filePath = Path.GetTempFileName();

        static string? fieldNameMapper(FieldInfo fieldInfo)
        {
            var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>();
            return attribute is not null ? attribute.Name : default;
        }

        static string? propertyNameMapper(PropertyInfo propertyInfo)
        {
            var attribute = propertyInfo.GetCustomAttribute<H5NameAttribute>();
            return attribute is not null ? attribute.Name : default;
        }

        var options = new H5SerializerOptions(
            IncludeStructProperties: type == typeof(WritingTestRecordStruct) || type == typeof(Dictionary<string, int>[]),
            FieldNameMapper: fieldNameMapper,
            PropertyNameMapper: propertyNameMapper
        );

        // Act
        file.Save(filePath, options);

        // Assert

        /* utf-8 is base8 encoded: https://stackoverflow.com/questions/75174726/hdf5-how-to-decode-utf8-encoded-string-from-h5dump-output*/
        var actual = TestUtils.DumpH5File(filePath);

        var suffix = type switch
        {
            Type when 
                type == typeof(bool)
                => $"_{data}",

            Type when 
                typeof(IDictionary).IsAssignableFrom(type) 
                => $"_{type.GenericTypeArguments[0].Name}_{type.GenericTypeArguments[1].Name}",

            Type when type != 
                typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(type) &&
                !type.IsArray
                => $"_{type.GenericTypeArguments[0].Name}",

            Type when
                type.IsGenericType &&
                typeof(Memory<>).Equals(type.GetGenericTypeDefinition())
                => $"_{type.GenericTypeArguments[0].Name}",

            _ => default
        };

        var expected = File
            .ReadAllText($"DumpFiles/attribute_{type.Name}{suffix}.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);

        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    [Theory]
    [MemberData(nameof(CommonData_FixedLengthString))]
    public void CanWriteCommon_DefaultFixedLengthString(object data)
    {
        // Arrange
        var type = data.GetType();
        var file = new H5File();
        file.Attributes[type.Name] = data;

        var filePath = Path.GetTempFileName();

        var options = new H5SerializerOptions(
            DefaultStringLength: 6
        );

        // Act
        file.Save(filePath, options);

        // Assert

        /* utf-8 is base8 encoded: https://stackoverflow.com/questions/75174726/hdf5-how-to-decode-utf8-encoded-string-from-h5dump-output*/
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText($"DumpFiles/attribute_default_fls_{type.Name}.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);

        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    [Theory]
    [MemberData(nameof(CommonData_FixedLengthStringMapper))]
    public void CanWriteCommon_FixedLengthStringMapper(object data)
    {
        // Arrange
        var type = data.GetType();
        var file = new H5File();
        file.Attributes[type.Name] = data;

        var filePath = Path.GetTempFileName();

        static int? fieldStringLengthMapper(FieldInfo fieldInfo)
        {
            var attribute = fieldInfo.GetCustomAttribute<H5StringLengthAttribute>();
            return attribute is not null ? attribute.Length : default;
        }

        static int? propertyStringLengthMapper(PropertyInfo propertyInfo)
        {
            var attribute = propertyInfo.GetCustomAttribute<H5StringLengthAttribute>();
            return attribute is not null ? attribute.Length : default;
        }

        var options = new H5SerializerOptions(
            DefaultStringLength: 3,
            FieldStringLengthMapper: fieldStringLengthMapper,
            PropertyStringLengthMapper: propertyStringLengthMapper
        );

        // Act
        file.Save(filePath, options);

        // Assert

        /* utf-8 is base8 encoded: https://stackoverflow.com/questions/75174726/hdf5-how-to-decode-utf8-encoded-string-from-h5dump-output*/
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText($"DumpFiles/attribute_default_flsm_{type.Name}.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);

        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    [Fact]
    public void CanWriteAttribute_Anonymous()
    {
        // Arrange
        var file = new H5File();

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

#if NET6_0_OR_GREATER
    [Fact]
    public void CanWriteAttribute_MultiDimensionalArray_value_type()
    {
        // Arrange
        var file = new H5File();

        var data = new int[,,]
        {
            {
                {  0,  1,  2 },
                {  3,  4,  5 },
                {  6,  7,  8 }
            },
            {
                {  9, 10, 11 },
                { 12, 13, 14 },
                { 15, 16, 17 }
            },
            {
                { 18, 19, 20 },
                { 21, 22, 23 },
                { 24, 25, 26 }
            },
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

# endif

    [Fact]
    public void CanWriteAttribute_MultiDimensionalArray_reference_type()
    {
        // Arrange
        var file = new H5File();

        var data = new string[,,]
        {
            {
                { "A", "B", "C" },
                { "D", "E", "F" },
                { "G", "H", "I" }
            },
            {
                { "J", "K", "L" },
                { "M", "N", "O" },
                { "P", "Q", "R" }
            },
            {
                { "S", "T", "U" },
                { "V", "W", "X" },
                { "Y", "Z", "Ä" }
            },
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
        var file = new H5File();

        foreach (var data in WritingTestData.Numerical)
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

#if NET7_0_OR_GREATER
    [Fact]
    public void CanWriteAttribute_Large_Array_Int128()
    {
        // Arrange
        var file = new H5File();

        foreach (var data in WritingTestData.Numerical_Int128)
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
            .ReadAllText("DumpFiles/attribute_large_array_int128.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }
#endif

    [Fact]
    public void CanWriteAttribute_2D()
    {
        // Arrange
        var file = new H5File();
        var data = new int[9] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var dimensions = new ulong[] { 3, 3 };

        file.Attributes["2D"] = new H5Attribute(data, dimensions);

        var filePath = Path.GetTempFileName();

        // Act
        file.Save(filePath);

        // Assert
        var actual = TestUtils.DumpH5File(filePath);

        var expected = File
            .ReadAllText($"DumpFiles/attribute_2D.dump")
            .Replace("<file-path>", filePath);

        Assert.Equal(expected, actual);
    }
}
using System.Collections;
using System.Reflection;
using PureHDF.Tests.Reading;
using Xunit;

namespace PureHDF.Tests.Writing;

public class DatasetTests
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

        var file = new H5File
        {
            [type.Name] = data
        };

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
            .ReadAllText($"DumpFiles/data_{type.Name}{suffix}.dump")
            .Replace("<file-path>", filePath)
            .Replace("<type>", "DATASET");

        Assert.Equal(expected, actual);

        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
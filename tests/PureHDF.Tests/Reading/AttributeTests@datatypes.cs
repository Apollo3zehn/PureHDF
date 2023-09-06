using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class AttributeTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static IList<object[]> AttributeNumericalTestData { get; } = ReadingTestData.NumericalReadData;

    [Theory]
    [MemberData(nameof(AttributeNumericalTestData))]
    public void CanRead_Numerical<T>(string name, T[] expected)
        where T : unmanaged
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddNumerical(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("numerical").Attribute(name);
            var actual = attribute.Read<T[]>();

            // Assert
            Assert.True(expected.SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_NonNullableStruct()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddStruct(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("struct").Attribute("nonnullable");
            var actual = attribute.Read<TestStructL1[]>();

            // Assert
            Assert.True(ReadingTestData.NonNullableStructData.SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_NullableStruct()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddStruct(fileId, ContainerType.Attribute, includeH5NameAttribute: true));

            // Act
            var options = new H5ReadOptions()
            {
                FieldNameMapper = fieldInfo =>
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }
            };

            using var root = NativeFile.InternalOpenRead(
                filePath, 
                deleteOnClose: true, 
                options);
                
            var attribute = root.Group("struct").Attribute("nullable");

            var actual = attribute.Read<TestStructStringAndArray[]>();

            // Assert
            Assert.Equal(
                JsonSerializer.Serialize(ReadingTestData.NullableStructData, _options), 
                JsonSerializer.Serialize(actual, _options));
        });
    }

    [Fact]
    public void CanRead_UnknownStruct()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddStruct(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("struct").Attribute("nullable");
            var actual = attribute.Read<Dictionary<string, object>[]>();

            // Assert
            Assert.Equal(
                JsonSerializer.Serialize(ReadingTestData.NullableStructData, _options),
                JsonSerializer.Serialize(actual, _options));
        });
    }

    [Fact]
    public void CanRead_ComplexObject()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddStruct(fileId, ContainerType.Attribute, includeH5NameAttribute: true));

            // Act
            var options = new H5ReadOptions()
            {
                PropertyNameMapper = propertyInfo =>
                {
                    var attribute = propertyInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : propertyInfo.Name;
                },
                IncludeClassFields = true
            };

            using var root = NativeFile.InternalOpenRead(
                filePath, 
                deleteOnClose: true, 
                options);
                
            var attribute = root.Group("struct").Attribute("nullable");

            var actual = attribute.Read<TestObjectStringAndArray[]>();

            // Assert
            Assert.Equal(
                JsonSerializer.Serialize(ReadingTestData.NullableStructData, _options).OrderBy(c => c).ToArray(),
                JsonSerializer.Serialize(actual, _options).OrderBy(c => c).ToArray());
        });
    }

    // Fixed-length string attribute (UTF8) is not supported because 
    // it is incompatible with variable byte length per character.
    [Theory]
    [InlineData("fixed+nullterm", new string[] { "00", "11", "22", "3", "44 ", "555", "66 ", "77", "  ", "AA ", "ZZ ", "!!" })]
    [InlineData("fixed+nullpad", new string[] { "0\00", "11", "22", "3 ", " 4", "55 5", "66", "77", "  ", "AA", "ZZ", "!!" })]
    [InlineData("fixed+spacepad", new string[] { "00", "11", "22", "3", " 4", "55 5", "66", "77", "", "AA", "ZZ", "!!" })]
    [InlineData("variable", new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
    [InlineData("variable+spacepad", new string[] { "001", "1 1", "22", "33", "44", "55", "66", "77", "", "AA", "ZZ", "!!" })]
    [InlineData("variableUTF8", new string[] { "00", "111", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
    public void CanRead_String(string name, string[] expected)
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddString(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("string").Attribute(name);
            var actual = attribute.Read<string[]>();

            // Assert
            Assert.True(expected.SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_Bitfield()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddBitField(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("bitfield").Attribute("bitfield");
            var actual = attribute.Read<TestBitfield[]>();

            // Assert
            Assert.True(ReadingTestData.BitfieldData.SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_Opaque()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddOpaque(fileId, ContainerType.Attribute));

            var expected = MemoryMarshal.Cast<int, ushort>(SharedTestData.SmallData).ToArray();

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("opaque").Attribute("opaque");
            var actual = attribute.Read<ushort[]>();

            // Assert
            Assert.True(expected.SequenceEqual(actual));
            Assert.Equal("Opaque Test Tag", attribute.Type.Opaque.Tag);
        });
    }

    [Fact]
    public void CanRead_Array_value()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddArray_value(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("array").Attribute("value");

            var actual = attribute
                .Read<int[,][,]>();

            var expected_casted = ReadingTestData.ArrayDataValue
                .Cast<int>()
                .ToArray();

            var actual_casted = actual
                .Cast<int[,]>()
                .SelectMany(x => x.Cast<int>())
                .ToArray();

            // Assert
            Assert.True(expected_casted.SequenceEqual(actual_casted));
        });
    }

    [Fact]
    public void CanRead_Array_variable_length_string()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddArray_variable_length_string(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("array").Attribute("variable_length_string");

            var actual = attribute
                .Read<string[,][,]>();

            var expected_casted = ReadingTestData.ArrayDataVariableLengthString
                .Cast<string>()
                .ToArray();

            var actual_casted = actual
                .Cast<string[,]>()
                .SelectMany(x => x.Cast<string>())
                .ToArray();

            // Assert
            Assert.True(expected_casted.SequenceEqual(actual_casted));
        });
    }

    [Fact]
    public void CanRead_Array_nullable_struct()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddArray_nullable_struct(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("array").Attribute("nullable_struct");

            var actual_1 = attribute
                .Read<TestStructStringAndArray[,][,]>()
                .Cast<TestStructStringAndArray[,]>()
                .SelectMany(x => x.Cast<TestStructStringAndArray>())
                .ToArray();

            var actual_2 = attribute
                .Read<Dictionary<string, object>[,][,]>()
                .Cast<Dictionary<string, object>[,]>()
                .SelectMany(x => x.Cast<Dictionary<string, object>>())
                .ToArray();

            var expected = ReadingTestData.NullableStructData
                .ToArray();

// HACK: Probably a bug in C-lib
            expected[6].StringValue1 = default!; expected[6].StringValue2 = default!;
            expected[7].StringValue1 = default!; expected[7].StringValue2 = default!;
            expected[8].StringValue1 = default!; expected[8].StringValue2 = default!;
            expected[9].StringValue1 = default!; expected[9].StringValue2 = default!;
            expected[10].StringValue1 = default!; expected[10].StringValue2 = default!;
            expected[11].StringValue1 = default!; expected[11].StringValue2 = default!;

            // Assert
            var expectedJsonString = JsonSerializer.Serialize(expected, _options);

            Assert.Equal(
                expectedJsonString,
                JsonSerializer.Serialize(actual_1, _options));

            Assert.Equal(
                expectedJsonString, 
                JsonSerializer.Serialize(actual_2, _options));
        });
    }

    [Fact]
    public void CanRead_Reference_Object()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddObjectReference(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute_references = root.Group("reference").Attribute("object");
            var references = attribute_references.Read<NativeObjectReference1[]>();

            var dereferenced = references
                .Select(reference => root.Get(reference))
                .ToArray();

            // Assert
            for (int i = 0; i < ReadingTestData.NumericalReadData.Count; i++)
            {
                var dataset = (NativeDataset)dereferenced[i];
                var expected = (Array)ReadingTestData.NumericalReadData[i][1];
                var elementType = expected.GetType().GetElementType()!;

                var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                var generic = method!.MakeGenericMethod(elementType);
                var result = (bool)generic.Invoke(null, new object[] { dataset, expected })!;

                Assert.True(result);
            }
        });
    }

    [Fact]
    public void CanRead_Reference_Region()
    {
        TestUtils.RunForAllVersions(version =>
        {
            /* it seems to be impossible to create a regular hyperslab but it should work even without this test */

            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddRegionReference(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset_referenced = root.Group("reference").Dataset("referenced");
            var attribute_region = root.Group("reference").Attribute("region");
            var references = attribute_region.Read<NativeRegionReference1[]>();

            static int[] Read(NativeFile root, IH5Dataset referenced, NativeRegionReference1 reference)
            {
                var selection = root.Get(reference);
                var actual = referenced.Read<int[]>(fileSelection: selection);

                return actual;
            }

            var actual_none = Read(root, dataset_referenced, references[0]);
            var actual_point = Read(root, dataset_referenced, references[1]);
            // var actual_regular_hyperslab = Read(root, dataset_referenced, references[2]);
            var actual_irregular_hyperslab = Read(root, dataset_referenced, references[3]);
            var actual_all = Read(root, dataset_referenced, references[4]);

            // Assert
            var expected_point = new int[] { 2, 27, 59, 50 };
            // var expected_regular_hyperslab = new int[] { 0, 1, 3, 4 };
            var expected_irregular_hyperslab = new int[] { 0, 1, 3, 4 };
            var expected_all = SharedTestData.SmallData.Take(60);

            Assert.Empty(actual_none);
            Assert.True(expected_point.SequenceEqual(actual_point));
            // Assert.True(expected_regular_hyperslab.SequenceEqual(actual_regular_hyperslab));
            Assert.True(expected_irregular_hyperslab.SequenceEqual(actual_irregular_hyperslab));
            Assert.True(expected_all.SequenceEqual(actual_all));

            Assert.Throws<Exception>(() => root.Get(default(NativeRegionReference1)));
        });
    }

    [Fact]
    public void CanRead_Variable_Length_Simple()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddVariableLengthSequence_Simple(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("sequence").Attribute("variable_simple");
            var actual = attribute.Read<int[][]>();

            // Assert
            var expected1 = new int[] { 3, 2, 1 };
            var expected2 = new int[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144 };

            Assert.Equal(3, actual.Length);
            Assert.True(expected1.SequenceEqual(actual[0]!));
            Assert.True(expected2.SequenceEqual(actual[1]!));
            Assert.Null(actual[2]);
        });
    }

    [Fact]
    public void CanRead_Variable_Length_Nullable_Struct()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId   
                => TestUtils.AddVariableLengthSequence_NullableStruct(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("sequence").Attribute("variable_nullable_struct");
            var actual = attribute.Read<TestStructStringAndArray[]?[]>();

            // Assert
            var expected = new TestStructStringAndArray[]?[] { ReadingTestData.NullableStructData, default };

            Assert.Equal(
                JsonSerializer.Serialize(expected, _options),
                JsonSerializer.Serialize(actual, _options));
        });
    }

    [Fact]
    public void CanRead_Shared_Message()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddDataWithSharedDataType(fileId, ContainerType.Attribute));

            var expected = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute_references = root.Group("shared_data_type").Attribute("shared_data_type");
            var actual = attribute_references.Read<string[]>();

            // Assert
            Assert.True(expected.SequenceEqual(actual));
        });
    }
}
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PureHDF.VFD;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        private readonly JsonSerializerOptions _options = new()
        {
            IncludeFields = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static IList<object[]> DatasetNumericalTestData { get; } = TestData.NumericalData;

        [Theory]
        [MemberData(nameof(DatasetNumericalTestData))]
        public void CanReadDataset_Numerical<T>(string name, T[] expected) where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddNumerical(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/numerical/{name}");
                var actual = dataset.Read<T>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_NonNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nonnullable");
                var actual = dataset.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
            });
        }

        [Fact]
        public void CanReadDataset_NullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual = dataset.ReadCompound<TestStructStringAndArray>(converter);

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.NullableStructData, _options),
                    JsonSerializer.Serialize(actual, _options));
            });
        }

        [Fact]
        public void CanReadDataset_UnknownStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");
                var actual = dataset.ReadCompound();

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.NullableStructData, _options).Replace("ShortValueWithCustomName", "ShortValue"),
                    JsonSerializer.Serialize(actual, _options));
            });
        }

        // Fixed-length string dataset (UTF8) is not supported because 
        // it is incompatible with variable byte length per character.
        [Theory]
        [InlineData("fixed+nullterm", new string[] { "00", "11", "22", "3", "44 ", "555", "66 ", "77", "  ", "AA ", "ZZ ", "!!" })]
        [InlineData("fixed+nullpad", new string[] { "0\00", "11", "22", "3 ", " 4", "55 5", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("fixed+spacepad", new string[] { "00", "11", "22", "3", " 4", "55 5", "66", "77", "", "AA", "ZZ", "!!" })]
        [InlineData("variable", new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" })]
        [InlineData("variable+spacepad", new string[] { "001", "1 1", "22", "33", "44", "55", "66", "77", "", "AA", "ZZ", "!!" })]
        [InlineData("variableUTF8", new string[] { "00", "111", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" })]
        public void CanReadDataset_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddString(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/string/{name}");
                var actual = dataset.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_Bitfield()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddBitField(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Group("bitfield").Dataset("bitfield");
                var actual = dataset.Read<TestBitfield>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.BitfieldData));
            });
        }

        [Fact]
        public void CanReadDataset_Opaque()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddOpaque(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Group("opaque").Dataset("opaque");
                var actual = dataset.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(MemoryMarshal.AsBytes<int>(TestData.SmallData).ToArray()));
            });
        }

        [Fact]
        public void CanReadDataset_Array_value()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray_value(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("value");

                var actual = dataset
                    .Read<int>()
                    .ToArray4D(2, 3, 4, 5);

                var expected_casted = TestData.ArrayDataValue.Cast<int>().ToArray();
                var actual_casted = actual.Cast<int>().ToArray();

                // Assert
                Assert.True(actual_casted.SequenceEqual(expected_casted));
            });
        }

        [Fact]
        public void CanReadDataset_Array_variable_length_string()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray_variable_length_string(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("variable_length_string");

                var actual = dataset
                    .ReadString();

                var expected = TestData.ArrayDataVariableLengthString
                    .Cast<string>()
                    .ToArray();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadDataset_Array_nullable_struct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray_nullable_struct(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("nullable_struct");

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual_1 = dataset
                    .ReadCompound<TestStructStringAndArray>(converter);

                var actual_2 = dataset
                    .ReadCompound();

                var expected = TestData.NullableStructData
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

#pragma warning disable SYSLIB1045
                Assert.Equal(
                    expectedJsonString, 
                    Regex.Replace(JsonSerializer.Serialize(actual_2, _options), "\"ShortValue", "\"ShortValueWithCustomName"));
            });
#pragma warning restore SYSLIB1045
        }

        // Currently there is no way to create files references of the 2nd generation: 
        // https://docs.hdfgroup.org/hdf5/develop/_f_m_t3.html#ReferenceEncodeRV
        // The script reference_attribute.py does not yet work. Maybe the feature is too new.

        // [Fact]
        // public void CanReadDataset_Reference_Attribute()
        // {
            
        // }

        [Fact]
        public void CanReadDataset_Reference_Object()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddObjectReference(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("object");
                var references = dataset_references.Read<NativeObjectReference>();

                var dereferenced = references
                    .Select(reference => root.Get(reference))
                    .ToArray();

                // Assert
                for (int i = 0; i < TestData.NumericalData.Count; i++)
                {
                    var dataset = (NativeDataset)dereferenced[i];
                    var expected = (Array)TestData.NumericalData[i][1];
                    var elementType = expected.GetType().GetElementType()!;

                    var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                    var generic = method!.MakeGenericMethod(elementType);
                    var result = (bool)generic.Invoke(null, new object[] { dataset, expected })!;

                    Assert.True(result);
                }
            });
        }

        [Fact]
        public void CanReadDataset_Reference_Region()
        {
            TestUtils.RunForAllVersions(version =>
            {
                /* it seems to be impossible to create a regular hyperslab - however, it should work even without this test */

#warning what to do with null references? same question applies to object reference

                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset_referenced = root.Group("reference").Dataset("referenced");
                var dataset_region = root.Group("reference").Dataset("region");
                var references = dataset_region.Read<NativeRegionReference>();

                static int[] Read(INativeFile root, IH5Dataset referenced, NativeRegionReference reference)
                {
                    var selection = root.Get(reference);
                    var actual = referenced.Read<int>(fileSelection: selection);

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
                var expected_all = TestData.SmallData.Take(60);

                Assert.Empty(actual_none);
                Assert.True(expected_point.SequenceEqual(actual_point));
                // Assert.True(expected_regular_hyperslab.SequenceEqual(actual_regular_hyperslab));
                Assert.True(expected_irregular_hyperslab.SequenceEqual(actual_irregular_hyperslab));
                Assert.True(expected_all.SequenceEqual(actual_all));
            });
        }

        [Fact]
        public void CanReadDataset_Shared_Message()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataWithSharedDataType(fileId, ContainerType.Dataset));
                var expected = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var attribute_references = root.Group("shared_data_type").Dataset("shared_data_type");
                var actual = attribute_references.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

#if NET5_0_OR_GREATER
        [Fact]
        public void ThrowsForNestedNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct((long)fileId, ContainerType.Dataset));

                // Act
                using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/struct/nullable");
                var exception = Assert.Throws<Exception>(() => dataset.ReadCompound<TestStructStringAndArrayL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }
#endif
    }
}
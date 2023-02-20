using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        private readonly JsonSerializerOptions _options = new()
        {
            IncludeFields = true,
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual = dataset.ReadCompound<TestStructStringAndArray>(converter);

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.StringStructData, _options),
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset("/struct/nullable");
                var actual = dataset.ReadCompound();

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.StringStructData, _options).Replace("ShortValueWithCustomName", "ShortValue"),
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Group("opaque").Dataset("opaque");
                var actual = dataset.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(MemoryMarshal.AsBytes<int>(TestData.SmallData).ToArray()));
            });
        }

        [Fact]
        public void CanReadDataset_Array()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Group("array").Dataset("array");

                var actual = dataset
                    .Read<int>()
                    .ToArray4D(2, 3, 4, 5);

                var expected_casted = TestData.ArrayData.Cast<int>().ToArray();
                var actual_casted = actual.Cast<int>().ToArray();

                var b = MemoryMarshal.AsBytes(expected_casted.AsSpan());
                var c = MemoryMarshal.AsBytes(actual_casted.AsSpan());

                // Assert
                Assert.True(actual_casted.SequenceEqual(expected_casted));
            });
        }

        [Fact]
        public void CanReadDataset_Reference_Object()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddObjectReference(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("object_reference");
                var references = dataset_references.Read<H5ObjectReference>();

                var dereferenced = references
                    .Select(reference => root.Get(reference))
                    .ToArray();

                // Assert
                for (int i = 0; i < TestData.NumericalData.Count; i++)
                {
                    var dataset = (H5Dataset)dereferenced[i];
                    var expected = (Array)TestData.NumericalData[i][1];
                    var elementType = expected.GetType().GetElementType()!;

                    var method = typeof(TestUtils).GetMethod(nameof(TestUtils.ReadAndCompare), BindingFlags.Public | BindingFlags.Static);
                    var generic = method!.MakeGenericMethod(elementType);
                    var result = (bool)generic.Invoke(null, new object[] { dataset, expected })!;

                    Assert.True(result);
                }
            });
        }

        [Fact(Skip = "Not yet fully implemented.")]
        public void CanReadDataset_Reference_Region()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddRegionReference(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset_references = root.Group("reference").Dataset("region_reference");
                var references = dataset_references.Read<H5RegionReference>();

                var reference = references[0];
                root.Context.Reader.Seek((long)reference.CollectionAddress, SeekOrigin.Begin);

                // H5Rint.c (H5R__get_region)
                // TODO: use more structs?
                var globalHeapId = new GlobalHeapId(root.Context)
                {
                    CollectionAddress = reference.CollectionAddress,
                    ObjectIndex = reference.ObjectIndex
                };

                var globalHeapCollection = globalHeapId.Collection;
                var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex];
                using var localReader = new H5StreamReader(new MemoryStream(globalHeapObject.ObjectData), leaveOpen: false);
                var address = root.Context.Superblock.ReadOffset(localReader);
                var selection = new DataspaceSelection(localReader);

                throw new NotImplementedException();
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
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
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var dataset = root.Dataset($"/struct/nullable");
                var exception = Assert.Throws<Exception>(() => dataset.ReadCompound<TestStructStringAndArrayL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }
#endif
    }
}
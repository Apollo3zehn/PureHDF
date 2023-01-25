using System.Buffers;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class AttributeTests
    {
        private readonly JsonSerializerOptions _options = new() { IncludeFields = true };
        public static IList<object[]> AttributeNumericalTestData { get; } = TestData.NumericalData;

        [Fact]
        public void CanReadAttribute_Dataspace_Scalar()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceScalar(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("dataspace").Attribute("scalar");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.SequenceEqual(new double[] { -1.2234234e-3 }));
            });
        }

        [Fact]
        public void CanReadAttribute_Dataspace_Null()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceNull(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("dataspace").Attribute("null");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.Length == 0);
            });
        }

        [Theory]
        [MemberData(nameof(AttributeNumericalTestData))]
        public void CanReadAttribute_Numerical<T>(string name, T[] expected)
            where T : unmanaged
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddNumerical(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("numerical").Attribute(name);
                var actual = attribute.Read<T>();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadAttribute_NonNullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("struct").Attribute("nonnullable");
                var actual = attribute.Read<TestStructL1>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
            });
        }

        [Fact]
        public void CanReadAttribute_NullableStruct()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("struct").Attribute("nullable");

                static string converter(FieldInfo fieldInfo)
                {
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    return attribute is not null ? attribute.Name : fieldInfo.Name;
                }

                var actual = attribute.ReadCompound<TestStructStringAndArray>(converter);

                // Assert
                Assert.Equal(JsonSerializer.Serialize(TestData.StringStructData, _options), JsonSerializer.Serialize(actual, _options));
            });
        }

        [Fact]
        public void CanReadAttribute_Unknown()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, (Action<long>)(fileId => TestUtils.AddStruct(fileId, ContainerType.Attribute)));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("struct").Attribute("nullable");
                var actual = attribute.ReadCompound();

                // Assert
                Assert.Equal(
                    JsonSerializer.Serialize(TestData.StringStructData, _options).Replace("ShortValueWithCustomName", "ShortValue"),
                    JsonSerializer.Serialize(actual, _options));
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
        public void CanReadAttribute_String(string name, string[] expected)
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddString(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("string").Attribute(name);
                var actual = attribute.ReadString();

                // Assert
                Assert.True(actual.SequenceEqual(expected));
            });
        }

        [Fact]
        public void CanReadAttribute_Bitfield()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddBitField(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("bitfield").Attribute("bitfield");
                var actual = attribute.Read<TestBitfield>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.BitfieldData));
            });
        }

        [Fact]
        public void CanReadAttribute_Opaque()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddOpaque(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("opaque").Attribute("opaque");
                var actual = attribute.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.SmallData));
            });
        }

        [Fact]
        public void CanReadAttribute_Array()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddArray(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("array").Attribute("array");
                var actual = attribute
                    .Read<int>()
                    .ToArray4D(2, 3, 4, 5);

                var expected_casted = TestData.ArrayData.Cast<int>().ToArray();
                var actual_casted = actual.Cast<int>().ToArray();

                // Assert
                Assert.True(actual_casted.SequenceEqual(expected_casted));
            });
        }

        [Fact]
        public void CanReadAttribute_Reference_Object()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddObjectReference(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute_references = root.Group("reference").Attribute("object_reference");
                var references = attribute_references.Read<H5ObjectReference>();

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

        [Fact]
        public void CanReadAttribute_Shared_Message()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataWithSharedDataType(fileId, ContainerType.Attribute));
                var expected = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute_references = root.Group("shared_data_type").Attribute("shared_data_type");
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
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddStruct(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("struct").Attribute("nullable");
                var exception = Assert.Throws<Exception>(() => attribute.ReadCompound<TestStructStringAndArrayL1>());

                // Assert
                Assert.Contains("Nested nullable fields are not supported.", exception.Message);
            });
        }
#endif

        [Fact]
        public void CanReadAttribute_Tiny()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddTiny(fileId, ContainerType.Attribute));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("tiny");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<byte>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.TinyData));
            });
        }

        [Fact]
        public void CanReadAttribute_Huge()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddHuge(fileId, ContainerType.Attribute, version));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("huge");
                var attribute = parent.Attributes.First();
                var actual = attribute.Read<int>();

                // Assert
                Assert.True(actual.SequenceEqual(TestData.HugeData[0..actual.Length]));
            });
        }

        [Fact]
        public void CanReadAttribute_MassAmount()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));
                var expectedCount = 1000;

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var parent = root.Group("mass_attributes");
                var attributes = parent.Attributes.ToList();

                foreach (var attribute in attributes)
                {
                    var actual = attribute.ReadCompound<TestStructL1>();

                    // Assert
                    Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
                }

                Assert.Equal(expectedCount, attributes.Count);
            });
        }
    }
}
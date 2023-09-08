using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class AttributeTests
{
    [Fact]
    public void CanRead_external_buffer_memory()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var expected = SharedTestData.SmallData;

            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.Add(
                    ContainerType.Attribute, fileId, "buffer", "memory",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("buffer").Attribute("memory");

            var actual = new int[expected.Length];
            attribute.Read(actual.AsMemory());

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        });
    }

    [Fact]
    public void CanRead_external_buffer_array()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var expected = SharedTestData.SmallData;

            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.Add(
                    ContainerType.Attribute, fileId, "buffer", "array",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("buffer").Attribute("array");

            var actual = new int[expected.Length];
            attribute.Read(actual);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        });
    }

    [Fact]
    public void CanRead_Tiny()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddTiny(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("tiny");
            var attribute = parent.Attributes().First();
            var actual = attribute.Read<byte[]>();

            // Assert
            Assert.True(SharedTestData.TinyData.SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_Huge()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddHuge(fileId, ContainerType.Attribute, version));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("huge");
            var attribute = parent.Attributes().First();
            var actual = attribute.Read<int[]>();

            // Assert
            Assert.True(SharedTestData.HugeData[0..actual.Length].SequenceEqual(actual));
        });
    }

    [Fact]
    public void CanRead_MassAmount()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddMass(fileId, ContainerType.Attribute));
            var expectedCount = 1000;

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("mass_attributes");
            var attributes = parent.Attributes().ToList();

            foreach (var attribute in attributes)
            {
                var actual = attribute.Read<TestStructL1[]>();

                // Assert
                Assert.True(ReadingTestData.NonNullableStructData.SequenceEqual(actual));
            }

            Assert.Equal(expectedCount, attributes.Count);
        });
    }
}
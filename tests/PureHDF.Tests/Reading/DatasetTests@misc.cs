using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class DatasetTests
{
    [Fact]
    public void CanRead_external_buffer_span()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var expected = SharedTestData.SmallData;

            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.Add(
                    ContainerType.Dataset, fileId, "buffer", "memory",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = (NativeDataset)root.Group("buffer").Dataset("memory");

            var actual = new int[expected.Length];
            dataset.Read(actual.AsSpan());

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        });
    }

    [Fact]
    public void CanRead_external_buffer_memory()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var expected = SharedTestData.SmallData;

            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId
                => TestUtils.Add(
                    ContainerType.Dataset, fileId, "buffer", "memory",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = root.Group("buffer").Dataset("memory");

            var actual = new int[expected.Length];
            dataset.Read(actual.AsMemory());

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
                    ContainerType.Dataset, fileId, "buffer", "array",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = root.Group("buffer").Dataset("array");

            var actual = new int[expected.Length];
            dataset.Read(actual);

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        });
    }
}
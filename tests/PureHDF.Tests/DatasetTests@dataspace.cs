using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        [Fact]
        public void CanReadDataset_Dataspace_Scalar()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceScalar(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("dataspace").Dataset("scalar");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.SequenceEqual(new double[] { -1.2234234e-3 }));
            });
        }

        [Fact]
        public void CanReadDataset_Dataspace_Null()
        {
            TestUtils.RunForAllVersions(version =>
            {
                // Arrange
                var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddDataspaceNull(fileId, ContainerType.Dataset));

                // Act
                using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
                var attribute = root.Group("dataspace").Dataset("null");
                var actual = attribute.Read<double>();

                // Assert
                Assert.True(actual.Length == 0);
            });
        }
    }
}
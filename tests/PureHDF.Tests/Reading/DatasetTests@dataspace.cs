using System.Reflection;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class DatasetTests
{
    [Fact]
    public void CanRead_Dataspace_Scalar()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddDataspaceScalar(fileId, ContainerType.Dataset));

            var expected = -1.2234234e-3;

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = root.Group("dataspace").Dataset("scalar");
            var actual = dataset.Read<double>();

            // Assert
            Assert.Equal(expected, actual);
        });
    }

    [Fact]
    public void CanRead_Dataspace_Null()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddDataspaceNull(fileId, ContainerType.Dataset));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = root.Group("dataspace").Dataset("null");

            void action() => dataset.Read<double[]>();

            // Assert
            Assert.Throws<TargetInvocationException>(action);
        });
    }   
}
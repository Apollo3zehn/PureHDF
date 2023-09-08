using System.Reflection;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class AttributeTests
{
    [Fact]
    public void CanRead_Dataspace_Scalar()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId 
                => TestUtils.AddDataspaceScalar(fileId, ContainerType.Attribute));

            var expected = -1.2234234e-3;

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("dataspace").Attribute("scalar");
            var actual = attribute.Read<double>();

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
                => TestUtils.AddDataspaceNull(fileId, ContainerType.Attribute));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var attribute = root.Group("dataspace").Attribute("null");

            void action() => attribute.Read<double[]>();

            // Assert
            Assert.Throws<TargetInvocationException>(action);
        });
    }
}
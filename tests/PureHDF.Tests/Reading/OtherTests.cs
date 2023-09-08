using System.Text;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public class OtherTests
    {
        [Fact]
        public void CanReadWrappedFile_Compact_sync()
        {
            // Arrange
            var filePath = "TestFiles/sine_compact.mat";

            // Act
            using var root = H5File.OpenRead(filePath);
            var result = root.Dataset("sine").Read<double[]>();
        }

        [Fact]
        public void CanReadWrappedFile_Chunked_sync()
        {
            // Arrange
            var filePath = "TestFiles/sine_chunked.mat";

            // Act
            using var root = H5File.OpenRead(filePath);
            var result = root.Dataset("sine").Read<double[]>();
        }

        [Theory]
        [InlineData("Deadbeef", 0x5c16ad42)]
        [InlineData("f", 0xb3e7e36f)]
        [InlineData("字形碼 / 字形码, Zìxíngmǎ", 0xfd18335c)]
        public void CanCalculateHash(string key, uint expected)
        {
            // Arrange
            var nameBytes = Encoding.UTF8.GetBytes(key);

            // Act
            var actual = ChecksumUtils.JenkinsLookup3(nameBytes);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
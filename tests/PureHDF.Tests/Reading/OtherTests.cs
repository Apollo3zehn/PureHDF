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
            var result = root.Dataset("sine").Read<double>();
        }

#if NET6_0_OR_GREATER
        [Fact]
        public async Task CanReadWrappedFile_Compact_async()
        {
            // Arrange
            var filePath = "TestFiles/sine_compact.mat";

            // Act
            using var root = H5File.OpenRead(filePath);
            var result = await root.Dataset("sine").ReadAsync<double>();
        }
#endif

        [Fact]
        public void CanReadWrappedFile_Chunked_sync()
        {
            // Arrange
            var filePath = "TestFiles/sine_chunked.mat";

            // Act
            using var root = H5File.OpenRead(filePath);
            var result = root.Dataset("sine").Read<double>();
        }

#if NET6_0_OR_GREATER
        [Fact]
        public async Task CanReadWrappedFile_Chunked_async()
        {
            // Arrange
            var filePath = "TestFiles/sine_chunked.mat";

            // Act
            using var root = H5File.OpenRead(filePath);
            var result = await root.Dataset("sine").ReadAsync<double>();
        }
#endif

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
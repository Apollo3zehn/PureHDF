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

            // Act
            var actual = ChecksumUtils.JenkinsLookup3(key);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanConvertDataset2D()
        {
            // Arrange
            var expected = new int[4, 7];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    expected[i, j] = i * j - j;
                }
            }

            // Act
            var actual = expected
                .Cast<int>()
                .ToArray()
                .ToArray2D(4, -1);

            // Assert
            Assert.Equal(expected.Rank, actual.Rank);

            for (int i = 0; i < expected.Rank; i++)
            {
                Assert.Equal(expected.GetLength(i), actual.GetLength(i));
            }

            Assert.True(actual.Cast<int>().SequenceEqual(expected.Cast<int>()));
        }

        [Fact]
        public void CanConvertDataset3D()
        {
            // Arrange
            var expected = new int[4, 7, 2];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    for (int k = 0; k < 2; k++)
                    {
                        expected[i, j, k] = i * j - j + k;
                    }
                }
            }

            // Act
            var actual = expected
                .Cast<int>()
                .ToArray()
                .ToArray3D(-1, 7, 2);

            // Assert
            Assert.Equal(expected.Rank, actual.Rank);

            for (int i = 0; i < expected.Rank; i++)
            {
                Assert.Equal(expected.GetLength(i), actual.GetLength(i));
            }

            Assert.True(actual.Cast<int>().SequenceEqual(expected.Cast<int>()));
        }
    }
}
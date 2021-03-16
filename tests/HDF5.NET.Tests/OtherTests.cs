using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class OtherTests
    {
        private readonly ITestOutputHelper _logger;

        public OtherTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public void CanReadWrappedFiles()
        {
            // Arrange
            var filePath = "testfiles/secret.mat";

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
            var children = root.Children.ToList();
        }

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
﻿using Xunit;

namespace PureHDF.Tests.Reading;

public partial class DatasetTests
{
    [Fact]
    public void CanRead_Compact()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddCompactDataset(fileId));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("compact");
            var dataset = parent.Dataset("compact");
            var actual = dataset.Read<int[]>();

            // Assert
            Assert.True(actual.SequenceEqual(SharedTestData.SmallData));
        });
    }

    [Fact]
    public void CanRead_CompactTestFile()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = "TestFiles/h5ex_d_compact.h5";
            var expected = new int[4, 7];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    expected[i, j] = i * j - j;
                }
            }

            // Act
            using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var parent = root;
            var dataset = parent.Dataset("DS1");
            var actual = dataset.Read<int[]>();

            // Assert
            Assert.True(actual.SequenceEqual(expected.Cast<int>()));
        });
    }
}
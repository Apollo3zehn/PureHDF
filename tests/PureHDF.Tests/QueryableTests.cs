using Xunit;
using static HDF.PInvoke.H5F;

namespace PureHDF.Tests.Reading
{
    public class QueryableTests
    {
        [Fact]
        public void CanQueryDataset_Full()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(libver_t.V110, (Action<long>)(fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset)));
            var expected = new int[] { 5, 6, 7, 10, 11, 12 };

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/small/small");

            var actual = dataset.AsQueryable<int>()
                .Skip(5)
                .Take(3)
                .Repeat(2)
                .Stride(5)
                .ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanQueryDataset_Skip_Take()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(libver_t.V110, (Action<long>)(fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset)));
            var expected = new int[] { 5, 6, 7 };

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/small/small");

            var actual = dataset.AsQueryable<int>()
                .Skip(5)
                .Take(3)
                .ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanQueryDataset_Skip_Stride()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(libver_t.V110, (Action<long>)(fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset)));
            var expected = Enumerable.Empty<int>();

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/small/small");

            var actual = dataset.AsQueryable<int>()
                .Skip(5)
                .Stride(3)
                .ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanQueryDataset_Skip()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(libver_t.V110, (Action<long>)(fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset)));

            var expected = Enumerable
                .Range(5, 95)
                .Select(value => value);

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/small/small");

            var actual = dataset.AsQueryable<int>()
                .Skip(5)
                .ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }

        [Fact]
        public void CanQueryDataset()
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(libver_t.V110, (Action<long>)(fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset)));

            var expected = Enumerable
                .Range(0, 100)
                .Select(value => value);

            // Act
            using var root = H5File.OpenReadCore(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/small/small");

            var actual = dataset.AsQueryable<int>()
                .ToArray();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        }
    }
}
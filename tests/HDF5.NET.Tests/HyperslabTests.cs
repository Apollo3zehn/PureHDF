using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace HDF5.NET.Tests.Reading
{
    public class HyperslabTests
    {
        // https://support.hdfgroup.org/HDF5/doc/H5.user/Chunking.html
        // https://support.hdfgroup.org/HDF5/doc/Advanced/Chunking/

        // Restrictions:
        // - The maximum number of elements in a chunk is 232 - 1 which is equal to 4,294,967,295.
        // - The maximum size for any chunk is 4GB.
        // - The size of a chunk cannot exceed the size of a fixed-size dataset. For example, a 
        //   dataset consisting of a 5x4 fixed-size array cannot be defined with 10x10 chunks.

        /*   DS [2 x 3 x 6]                        CHUNK [2 x 3 x 6]
         *                  |-05--11--17-|                        |<----------->|
         *               |-04--10--16-|                        |<----------->|
         *            |-03--09--15-|                        |<----------->|
         *         |-02--08--14-|                        |<----------->|
         *      |-01--07--13-|                        |<----------->|
         *   |-00--06--12-|                        |<-----01---->|
         *   |-18--24--30-|                        |<----------->|
         */

        /*   DS [2 x 3 x 6]                        CHUNK [1 x 2 x 3]
         *                  |-08--11--17-|                        |<------><----|
         *               |-07--10--16-|                        |<------><----|
         *            |-06--09--15-|                        |<--02--><-04-|
         *         |-02--05--14-|                        |<------><----|
         *      |-01--04--13-|                        |<------><----|
         *   |-00--03--12-|                        |<--01--><-03-|
         *   |-18--21--30-|                        |<--05--><-07-|
         */

        private readonly ITestOutputHelper _logger;

        public HyperslabTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public async Task CanCalulateProjections1()
        {
            // Arrange
            var datasetDims = new ulong[] { 2, 3, 6 };
            var chunkDims = new ulong[] { 1, 2, 3 }; // ulong ok?
            var memoryShape = new ulong[] { 4, 5, 6 };

            var slices = new Slice[]
            {
                new Slice { Start = 0, Stop = 1, Stride = 1 },
                new Slice { Start = 1, Stop = 2, Stride = 1 },
                new Slice { Start = 1, Stop = 4, Stride = 1 }
            };

            var settings = new HyperslabSettings(Rank: 3, datasetDims, chunkDims, memoryShape);
            var slabber = new Hyperslabber();

            // Act
            var projections = slabber.ComputeSliceProjections(settings, slices);

            // Assert

        }

        [Fact]
        public async Task CanCalulateProjections2()
        {
            // Arrange
            var datasetDims = new ulong[] { 20, 30, 40 };
            var chunkDims = new ulong[] { 6, 6, 6 }; // ulong ok?
            var memoryShape = new ulong[] { 100, 100, 100 };

            var slices = new Slice[]
            {
                new Slice { Start = 10, Stop = 20, Stride = 3 },
                new Slice { Start = 11, Stop = 21, Stride = 4 },
                new Slice { Start = 25, Stop = 39, Stride = 1 }
            };

            var settings = new HyperslabSettings(Rank: 3, datasetDims, chunkDims, memoryShape);
            var slabber = new Hyperslabber();

            // Act
            var projections = slabber.ComputeSliceProjections(settings, slices);

            // Assert

        }

        //[Fact]
        //public void CanReadHyperslab()
        //{
        //    TestUtils.RunForAllVersions(version =>
        //    {
        //        // Arrange
        //        var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddChunkedDatasetForHyperslab(fileId));

        //        // Act
        //        using var root = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, deleteOnClose: true);
        //        var parent = root.Group("chunked");
        //        var dataset = parent.Dataset("hyperslab");

        //        foreach (var attribute in attributes)
        //        {
        //            var actual = attribute.ReadCompound<TestStructL1>();

        //            // Assert
        //            Assert.True(actual.SequenceEqual(TestData.NonNullableStructData));
        //        }

        //        Assert.Equal(expectedCount, attributes.Count);
        //    });
        //}
    }
}
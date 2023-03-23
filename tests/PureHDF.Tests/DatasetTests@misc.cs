using System.Runtime.InteropServices;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading
{
    public partial class DatasetTests
    {
        [Fact]
        public void CanReadDataset_half_size_memory_selection_value()
        {
            /*
             *  file selection
             *     0   1   2   3   4   5   6   7   8   9 
             *  0  -   -   -   -   -   -   -   -   -   - 
             *  1  -   A   B   C   -   D   E   F   -   - 
             *  2  -   G   H   I   -   J   K   L   -   - 
             *  3  -   M   N   O   -   P   Q   R   -   - 
             *  4  -   -   -   -   -   -   -   -   -   - 
             *  5  -   S   T   U   -   V   W   X   -   - 
             *  6  -   Y   Z   A   -   B   C   D   -   - 
             *  7  -   E   F   G   -   H   I   J   -   - 
             *  8  -   -   -   -   -   -   -   -   -   -
             *  9  -   -   -   -   -   -   -   -   -   -
             */

            /*
             * memory selection
             *     0   1   2   3   4   5   6   7   8   9 
             *  0  -   -   -   -   -   -   -   -   -   - 
             *  1  -   A   B   -   C   D   -   E   F   - 
             *  2  -   G   H   -   I   J   -   K   L   - 
             *  3  -   -   -   -   -   -   -   -   -   - 
             *  4  -   M   N   -   O   P   -   Q   R   - 
             *  5  -   S   T   -   U   V   -   W   X   - 
             *  6  -   -   -   -   -   -   -   -   -   - 
             *  7  -   Y   Z   -   A   B   -   C   D   - 
             *  8  -   E   F   -   G   H   -   I   J   -
             *  9  -   -   -   -   -   -   -   -   -   -
             */

            // Arrange
            var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId 
                => TestUtils.Add(ContainerType.Dataset, fileId, "misc", "half_size", H5T.NATIVE_INT32, TestData.SmallData.AsSpan(), new ulong[] { 10, 10 }));

            var expected = new ushort[100]
                .ToArray2D(10, 10);

            expected[1, 1] = 11; expected[1, 2] = 12; expected[1, 4] = 13; expected[1, 5] = 15; expected[1, 7] = 16; expected[1, 8] = 17;
            expected[2, 1] = 21; expected[2, 2] = 22; expected[2, 4] = 23; expected[2, 5] = 25; expected[2, 7] = 26; expected[2, 8] = 27;

            expected[4, 1] = 31; expected[4, 2] = 32; expected[4, 4] = 33; expected[4, 5] = 35; expected[4, 7] = 36; expected[4, 8] = 37;
            expected[5, 1] = 51; expected[5, 2] = 52; expected[5, 4] = 53; expected[5, 5] = 55; expected[5, 7] = 56; expected[5, 8] = 57;

            expected[7, 1] = 61; expected[7, 2] = 62; expected[7, 4] = 63; expected[7, 5] = 65; expected[7, 7] = 66; expected[7, 8] = 67;
            expected[8, 1] = 71; expected[8, 2] = 72; expected[8, 4] = 73; expected[8, 5] = 75; expected[8, 7] = 76; expected[8, 8] = 77;

            var fileSeletion = new HyperslabSelection(rank: 2,
                starts: new ulong[] { 1, 1 },
                strides: new ulong[] { 4, 4 },
                counts: new ulong[] { 2, 2 },
                blocks: new ulong[] { 3, 3 });

            var memorySelection = new HyperslabSelection(rank: 2,
                starts: new ulong[] { 1, 1 },
                strides: new ulong[] { 3, 3 },
                counts: new ulong[] { 3, 3 },
                blocks: new ulong[] { 2, 2 });

            var memoryDims = new ulong[] { 10, 10 };

            // Act
            using var root = NativeFile.OpenRead(filePath, deleteOnClose: true);
            var dataset = root.Dataset($"/misc/half_size");

            var actual_ushort = dataset.Read<ushort>(
                fileSelection: fileSeletion, 
                memorySelection: memorySelection, 
                memoryDims: memoryDims);

            var actual = MemoryMarshal
                .Cast<ushort, int>(actual_ushort)
                .ToArray()
                .ToArray2D(10, 10);

            // Assert
            for (int i = 0; i < actual.GetLength(0); i++)
            {
                for (int j = 0; j < actual.GetLength(1); j++)
                {
                    Assert.Equal(expected[i, j], actual[i, j]);
                }
            }
        }
    }
}
using Xunit;

namespace PureHDF.Tests.Writing;

public class SelectionTests
{
    /* By default, the file element count default to the memory selection,
     * element count which itself default to the memory dims.
     */

    [Fact]
    public void CanWrite_MemorySelection_Point_1D()
    {
        /* - File element count is determined by the memory selection element count. */

        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset(
            data: data,
            memorySelection: new PointSelection(new ulong[,]
            {
                { 0 },
                { 1 },
                { 1 },
                { 2 },
                { 3 },
                { 5 },
                { 8 },
                { 13 }
            })
        );

        var file = new H5File
        {
            ["selection"] = dataset
        };

        var filePath = Path.GetTempFileName();
       
        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/selection_point_memory_1d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_MemorySelection_Hyperslab_2D()
    {
        /* - File element count is determined by the memory selection element count. */

        // Arrange
        var data = SharedTestData.SmallData;

        var data2D = new int[10, 10];

        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                data2D[i, j] = data[i * 10 + j];
            }
        }

        var dataset = new H5Dataset(
            data: data2D,
            memorySelection: new HyperslabSelection(rank: 2,
                starts: new ulong[] { 1, 2 },
                strides: new ulong[] { 4, 4 },
                counts: new ulong[] { 2, 2 },
                blocks: new ulong[] { 3, 3 })
        );

        var file = new H5File
        {
            ["selection"] = dataset
        };

        var filePath = Path.GetTempFileName();
       
        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/selection_hyperslab_memory_2d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_FileSelection_Point_1D()
    {
        /* - Additional memory selection is required so that
         *   both selections have the same element count.
         *
         * - File element count is determined by the memory selection element count.
         */

        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset(
            data: data,
            memorySelection: new PointSelection(new ulong[,]
            {
                { 0 },
                { 0 },
                { 1 },
                { 1 },
                { 2 },
                { 2 },
                { 3 },
                { 3 }
            }),
            fileSelection: new PointSelection(new ulong[,]
            {
                { 7 },
                { 6 },
                { 5 },
                { 4 },
                { 3 },
                { 2 },
                { 1 },
                { 0 }
            })
        );

        var file = new H5File
        {
            ["selection"] = dataset
        };

        var filePath = Path.GetTempFileName();
       
        // Act
        file.Write(filePath);

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/selection_point_file_1d.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
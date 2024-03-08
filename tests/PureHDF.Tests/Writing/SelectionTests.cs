using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Writing;

public class SelectionTests
{
    /* (1) The file space defaults to the memory space if nothing
     * else is provided or an AllSelection is provided for the
     * memory.

     * (2) If any other memory selection is provided, the file
     * space defaults to the memory selection element count (1D).
     * This ensures that the auto-generated file selection has the
     * same size as the memory selection and that the file space
     * is not larger than necessary.
     *
     * (3) If a file selection is provided, the file space still
     * defaults to the memory selection element count or the memory
     * space. Usually it makes sense for the user to also provide
     * a file space explicitly.
     *
     * (4) If both selections are provided, file and memory
     * selections, they need to have the same number of total
     * elements.
     */

    [Fact]
    public void CanWrite_MemorySelection_All_2D()
    {
        /* - File space is determined by the memory space. */

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
            memorySelection: new AllSelection()
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
                .ReadAllText("DumpFiles/selection_all_memory_2d.dump")
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
    public void CanWrite_MemorySelection_Point_1D()
    {
        /* - File space is determined by the memory selection element count. */

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
        /* - File space is determined by the memory selection element count. */

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
    public void CanWrite_MemorySelection_Hyperslab_2D_Deferred()
    {
        /* - File space is provided explicitly. */

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

        var dataset = new H5Dataset<int[,]>(
            fileDims: new ulong[] { 36 }
        );

        var file = new H5File
        {
            ["selection"] = dataset
        };

        var filePath = Path.GetTempFileName();

        // Act
        using (var writer = file.BeginWrite(filePath))
        {
            writer.Write(
                dataset,
                data: data2D,
                memorySelection: new HyperslabSelection(rank: 2,
                    starts: new ulong[] { 1, 2 },
                    strides: new ulong[] { 4, 4 },
                    counts: new ulong[] { 2, 2 },
                    blocks: new ulong[] { 3, 3 })
            );
        };

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
        /* - Additional memory selection is provided so that
         *   both selections have the same element count.
         *
         * - File space is determined by the memory selection element count.
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

    [Fact]
    public void CanWrite_FileSelection_Hyperslab_2D()
    {
        /* - Additional memory selection is provided so that
         *   both selections have the same element count.
         *
         * - Additional file dims are provided so that the 
         *   file space is large enough for the file selection.
         */

        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset(

            data: data,

            memorySelection: new HyperslabSelection(
                start: 0,
                block: 36),

            fileSelection: new HyperslabSelection(rank: 2,
                starts: new ulong[] { 1, 2 },
                strides: new ulong[] { 4, 4 },
                counts: new ulong[] { 2, 2 },
                blocks: new ulong[] { 3, 3 }),

            fileDims: new ulong[] { 10, 10 }

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
                .ReadAllText("DumpFiles/selection_hyperslab_file_2d.dump")
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
    public void CanWrite_FileSelection_Hyperslab_2D_Deferred()
    {
        /* - Additional memory selection is provided so that
         *   both selections have the same element count.
         *
         * - Additional file dims are provided so that the 
         *   file space is large enough for the file selection.
         */

        // Arrange
        var data = SharedTestData.SmallData;

        var dataset = new H5Dataset<int[]>(
            fileDims: new ulong[] { 10, 10 }
        );

        var file = new H5File
        {
            ["selection"] = dataset
        };

        var filePath = Path.GetTempFileName();

        // Act
        using (var writer = file.BeginWrite(filePath))
        {
            writer.Write(

                dataset,

                data: data,

                memorySelection: new HyperslabSelection(
                    start: 0,
                    block: 36),

                fileSelection: new HyperslabSelection(rank: 2,
                    starts: new ulong[] { 1, 2 },
                    strides: new ulong[] { 4, 4 },
                    counts: new ulong[] { 2, 2 },
                    blocks: new ulong[] { 3, 3 })
            );
        };

        // Assert
        try
        {
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/selection_hyperslab_file_2d.dump")
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
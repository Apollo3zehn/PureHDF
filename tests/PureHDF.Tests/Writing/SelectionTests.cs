using Xunit;

namespace PureHDF.Tests.Writing;

public class SelectionTests
{
    // [Fact]
    // public void CanWrite_Selection()
    // {
    //     // Arrange
    //     var data = SharedTestData.SmallData;

    //     var dataset = new H5Dataset<int[]>(
    //         fileSelection: new PointSelection()
    //     );

    //     var file = new H5File
    //     {
    //         ["selection"] = dataset
    //     };

    //     var filePath = Path.GetTempFileName();
       
    //     // Act
    //     using (var writer = file.BeginWrite(filePath, options))
    //     {
    //         writer.Write(dataset, data);
    //     }

    //     // Assert
    //     try
    //     {
    //         var actual = TestUtils.DumpH5File(filePath);

    //         var expected = File
    //             .ReadAllText("DumpFiles/layout_chunked_1d.dump")
    //             .Replace("<file-path>", filePath);

    //         Assert.Equal(expected, actual);
    //         CheckIndexType<SingleChunkIndexingInformation>(filePath, filtered: true);
    //     }
    //     finally
    //     {
    //         if (File.Exists(filePath))
    //             File.Delete(filePath);
    //     }
    // }
}
using Xunit;

namespace PureHDF.Tests.RoundTrip;

public class DatasetsRoundTripTests
{
    [Fact]
    public void WriteAndReadOpaqueDataset()
    {
        // Arrange
        var h5FileWrite = new H5File();
        var expected = new byte[] { 0x01, 0x02, 0x13 };

        h5FileWrite["test"] = new H5Dataset(
            data: expected, 
            opaqueInfo: new H5OpaqueInfo((uint) expected.Length, "New")
        );

        var memoryStream = new MemoryStream();

        h5FileWrite.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var h5FileRead = H5File.Open(memoryStream);
        var dataset = h5FileRead.Dataset("test");

        // Act
        var actual = dataset.Read<byte[]>();

        // Assert
        Assert.True(expected.SequenceEqual(actual));
    }
}
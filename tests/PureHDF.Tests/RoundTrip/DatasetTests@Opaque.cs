using Xunit;

namespace PureHDF.Tests.RoundTrip;

public class DataSetsRoundTripTests
{
    [Fact]
    public void WriteAndReadOpaqueDataset()
    {
        var reproducibleProblem = new H5File();

        var data = new byte[] { 0x01, 0x02, 0x13 };
        reproducibleProblem["test"] = new H5Dataset(data, opaqueInfo: new H5OpaqueInfo((uint) data.Length, "New"));

        var memoryStream = new MemoryStream();

        reproducibleProblem.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var open = H5File.Open(memoryStream);
        var dataset = open.Dataset("test");

        var dataRead = dataset.Read<byte[]>();
        Assert.True(dataRead.SequenceEqual(data));
    }
}
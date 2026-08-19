using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading.VFD;

// H5MemoryDriver is the driver behind H5File.Open(ReadOnlyMemory<byte>) and its async twin. It reads
// a fixed in-memory buffer by slicing it directly - no Stream, no memory-mapped view, no cursor to
// share - so it is concurrent by construction and never suspends. These tests cover its contract:
//
//   1. A plain read decodes the right bytes (the basic contract).
//   2. Variable-length data resolves through the global heap, which seeks the driver mid-read; a
//      driver that shared a cursor would corrupt the collection decode silently.
//
// The async overload is covered for parity: it exists for a caller written entirely against the
// async surface, but the buffer is a synchronous source so it must agree with the synchronous open.
//
// Concurrency - parallel reads through one H5File - is exercised in ConcurrencyTests, alongside the
// other drivers, rather than per driver here.
[Collection(SharedHdf5StateCollection.Name)]
public class H5MemoryDriverTests
{
    private static readonly string[] VariableLengthExpected =
    [
        "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
    ];

    [Fact]
    public void CanReadDatasetFromInMemoryBuffer()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            TestUtils.AddChunkedDataset_Huge);

        var fileBytes = ReadAllBytesAndDelete(filePath);

        // Act
        using var root = H5File.Open((ReadOnlyMemory<byte>)fileBytes);

        var actual = root
            .Group("chunked")
            .Dataset("chunked_huge")
            .Read<int[]>();

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.HugeData));
    }

    [Fact]
    public void CanReadVariableLengthDatasetFromInMemoryBuffer()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddString(fileId, ContainerType.Dataset));

        var fileBytes = ReadAllBytesAndDelete(filePath);

        // Act
        using var root = H5File.Open((ReadOnlyMemory<byte>)fileBytes);

        var actual = root
            .Group("string")
            .Dataset("variable")
            .Read<string[]>();

        // Assert
        Assert.Equal(VariableLengthExpected, actual);
    }

    [Fact]
    public async Task OpenAsyncWithInMemoryBufferAgreesWithOpen()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset));

        var fileBytes = File.ReadAllBytes(filePath);

        // Act
        using var syncRoot = H5File.Open((ReadOnlyMemory<byte>)fileBytes);
        var expected = syncRoot.Dataset("small/small").Read<int[]>();

        using var asyncRoot = await H5File.OpenAsync((ReadOnlyMemory<byte>)fileBytes);
        var actual = await (await asyncRoot.DatasetAsync("small/small")).ReadAsync<int[]>();

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(SharedTestData.SmallData, actual);
    }

    [Fact]
    public async Task OpenAsyncWithInMemoryBufferObservesAnAlreadyCancelledToken()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset));

        var fileBytes = ReadAllBytesAndDelete(filePath);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => H5File.OpenAsync((ReadOnlyMemory<byte>)fileBytes, cancellationToken: cts.Token));
    }

    private static byte[] ReadAllBytesAndDelete(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}

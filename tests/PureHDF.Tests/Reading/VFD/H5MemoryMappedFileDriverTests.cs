using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading.VFD;

// H5MemoryMappedFileDriver is the driver behind H5File.Open(MemoryMappedViewAccessor) and its async
// twin. It reads a memory-mapped view by copying from an acquired pointer - no Stream, no cursor to
// share - so it is concurrent by construction and never suspends. These tests cover its contract:
//
//   1. A plain read decodes the right bytes (the basic contract).
//   2. Variable-length data resolves through the global heap, which seeks the driver mid-read; a
//      driver that shared a cursor would corrupt the collection decode silently.
//
// The async overload is covered for parity: it exists for a caller written entirely against the
// async surface, but a memory-mapped view is a synchronous source so it must agree with the
// synchronous open. H5File.Open(accessor) takes ownership of the accessor and disposes it when the
// returned file is disposed, so each test creates its own accessor pair from its own file.
//
// Concurrency - parallel reads through one H5File - is exercised in ConcurrencyTests, alongside the
// other drivers, rather than per driver here.
[Collection(SharedHdf5StateCollection.Name)]
public class H5MemoryMappedFileDriverTests
{
    private static readonly string[] VariableLengthExpected =
    [
        "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
    ];

    [Fact]
    public void CanReadDatasetFromMemoryMappedFile()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            TestUtils.AddChunkedDataset_Huge);

        // Act
        int[] actual;
        using (var mmf = MemoryMappedFile.CreateFromFile(filePath))
        using (var accessor = mmf.CreateViewAccessor())
        using (var root = H5File.Open(accessor))
        {
            actual = root
                .Group("chunked")
                .Dataset("chunked_huge")
                .Read<int[]>();
        }

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.HugeData));
    }

    [Fact]
    public void CanReadVariableLengthDatasetFromMemoryMappedFile()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddString(fileId, ContainerType.Dataset));

        // Act
        string[] actual;
        using (var mmf = MemoryMappedFile.CreateFromFile(filePath))
        using (var accessor = mmf.CreateViewAccessor())
        using (var root = H5File.Open(accessor))
        {
            actual = root
                .Group("string")
                .Dataset("variable")
                .Read<string[]>();
        }

        // Assert
        Assert.Equal(VariableLengthExpected, actual);
    }

    [Fact]
    public async Task OpenAsyncWithMemoryMappedViewAccessorAgreesWithOpen()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset));

        // Act
        int[] expected;
        using (var mmf = MemoryMappedFile.CreateFromFile(filePath))
        using (var accessor = mmf.CreateViewAccessor())
        using (var syncRoot = H5File.Open(accessor))
        {
            expected = syncRoot.Dataset("small/small").Read<int[]>();
        }

        int[] actual;
        using (var mmf = MemoryMappedFile.CreateFromFile(filePath))
        using (var accessor = mmf.CreateViewAccessor())
        using (var asyncRoot = await H5File.OpenAsync(accessor))
        {
            actual = await (await asyncRoot.DatasetAsync("small/small")).ReadAsync<int[]>();
        }

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(SharedTestData.SmallData, actual);
    }

    [Fact]
    public async Task OpenAsyncWithMemoryMappedViewAccessorObservesAnAlreadyCancelledToken()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset));

        using var mmf = MemoryMappedFile.CreateFromFile(filePath);
        using var accessor = mmf.CreateViewAccessor();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => H5File.OpenAsync(accessor, cancellationToken: cts.Token));
    }
}

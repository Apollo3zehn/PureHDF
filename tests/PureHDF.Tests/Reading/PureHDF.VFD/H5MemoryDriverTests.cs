using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading.VFD;

// H5MemoryDriver is the driver behind H5File.Open(ReadOnlyMemory<byte>) and its async twin. It reads
// a fixed in-memory buffer by slicing it directly - no Stream, no memory-mapped view, no cursor to
// share - so it is concurrent by construction and never suspends. These tests cover the three things
// that could break independently:
//
//   1. A plain read decodes the right bytes (the basic contract).
//   2. Concurrent reads through one H5File stay correct - the per-operation driver carries its own
//      position over the same buffer, so two threads slicing the same Span must not collide.
//   3. Variable-length data resolves through the global heap, which seeks the driver mid-read; a
//      driver that shared a cursor would corrupt the collection decode silently.
//
// The async overload is covered for parity: it exists for a caller written entirely against the
// async surface, but the buffer is a synchronous source so it must agree with the synchronous open.
[Collection(SharedHdf5StateCollection.Name)]
public class H5MemoryDriverTests
{
    private const int CHUNK_SIZE = 1_000_000;

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
    public void CanReadDatasetParallel_InMemoryBuffer()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            TestUtils.AddChunkedDataset_Huge);

        var fileBytes = ReadAllBytesAndDelete(filePath);

        // Act
        using var root = H5File.Open((ReadOnlyMemory<byte>)fileBytes);

        // resolved once, on this thread - this test covers concurrent READS
        var dataset = root.Group("chunked").Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData
                .AsSpan(i * CHUNK_SIZE, CHUNK_SIZE)
                .ToArray();

            Assert.True(actual.SequenceEqual(slicedData));
        });
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

    // The async memory-mapped overload exists for symmetry: a caller written entirely against the
    // async surface should not have to special-case one driver. It uses the same H5MemoryMappedFileDriver
    // as the synchronous overload, so this is a guard that the async entry point wires it up rather
    // than skipping it.
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

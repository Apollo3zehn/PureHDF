using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading.Async;

/// <summary>
/// Covers <c>NativeDataset.ReadAsync</c>, which threw <see cref="NotImplementedException" /> until the
/// decode path below it was converted.
/// </summary>
/// <remarks>
/// Each test asserts PARITY with the synchronous <c>Read</c> - the two share one implementation and
/// differ only in whether the public boundary blocks, so a divergence means the shared core broke for
/// one of them.
/// <para>
/// The four data layouts are covered separately because they resolve their source stream in genuinely
/// different ways, and only one of them reads while doing so: compact data is already in the object
/// header, contiguous data only needs the driver positioned, a chunk index has to be consulted and the
/// chunk itself fetched (and possibly decompressed), and a virtual dataset delegates to other
/// datasets. That last one gathers through <c>Memory</c> and so suspends like the rest - see
/// <see cref="ReadAsyncOfAVirtualDatasetWorksWhenEveryReadSuspends" />.
/// </para>
/// <para>
/// WHAT THESE TESTS DO NOT PROVE: that the async path never blocks - see the same note on
/// <c>AsyncNavigationTests</c>. A test host always has a thread pool to resume continuations on, so a
/// leftover bridge would still pass here.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class AsyncDatasetReadTests
{
    [Fact]
    public async Task ReadAsyncMatchesReadForCompactData()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, TestUtils.AddCompactDataset);

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = root.Dataset("compact/compact");

        // Act
        var actual = await dataset.ReadAsync<int[]>();

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.SmallData));
        Assert.Equal(dataset.Read<int[]>(), actual);
    }

    [Fact]
    public async Task ReadAsyncMatchesReadForContiguousData()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, TestUtils.AddContiguousDataset);

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = root.Dataset("contiguous/contiguous");

        // Act
        var actual = await dataset.ReadAsync<int[]>();

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.HugeData));
    }

    /// <summary>
    /// The chunked layout across all of its index forms - the case that motivated giving
    /// <c>IReadingChunkCache</c> an async twin, because this is where a read actually happens while
    /// resolving the source stream.
    /// </summary>
    [Theory]
    [InlineData("b-tree v1")]
    [InlineData("b-tree v2")]
    [InlineData("fixed array")]
    [InlineData("fixed array, paged")]
    [InlineData("extensible array, elements")]
    [InlineData("extensible array, data blocks")]
    [InlineData("single chunk")]
    public async Task ReadAsyncMatchesReadForChunkedData(string indexForm)
    {
        foreach (var withShuffle in new[] { false, true })
        {
            // Arrange
            var (build, datasetName) = ChunkedCase(indexForm, withShuffle);

            var version = indexForm == "b-tree v1"
                ? H5F.libver_t.V18
                : H5F.libver_t.LATEST;

            var filePath = TestUtils.PrepareTestFile(version, build);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var dataset = root.Group("chunked").Dataset(datasetName);

            // Act
            var actual = await dataset.ReadAsync<int[]>();

            // Assert
            Assert.True(actual.SequenceEqual(SharedTestData.MediumData));
        }
    }

    /// <summary>
    /// A filtered dataset, so that the decompression step inside the async chunk read is exercised
    /// too - the filter pipeline runs after the await, on the buffer it produced.
    /// </summary>
    [Fact]
    public async Task ReadAsyncMatchesReadForFilteredData()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, TestUtils.AddFilteredDataset_ZLib);

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = root.Group("filtered").Dataset("deflate");

        // Act
        var actual = await dataset.ReadAsync<int[]>();

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.MediumData));
    }

    /// <summary>
    /// A virtual dataset gathers from other datasets, and the gather is awaited rather than blocked on.
    /// </summary>
    /// <remarks>
    /// The trailing <c>-1</c> pair is the fill value. Note WHY: the fixture maps a third source file
    /// and deliberately never creates it, so that pair is an UNRESOLVABLE source rather than an
    /// unmapped region. The two are indistinguishable here, which is the point - an unreachable source
    /// silently produces plausible data.
    /// <para>
    /// The other two sources are separate .h5 files written as bare relative paths into the process
    /// working directory, so they resolve by probing the local filesystem. That makes this test
    /// filesystem-dependent, and it says nothing about reading a virtual dataset from a non-filesystem
    /// source - see <see cref="VirtualDatasetSameFileTests" /> for the case that does.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ReadAsyncOfAVirtualDatasetGathersFromItsSources()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.V110,
            fileId => TestUtils.AddVirtualDataset(fileId, "virtual"));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: false);
        var dataset = root.Dataset("vds");
        var selection = new HyperslabSelection(start: 3, stride: 4, count: 4, block: 2);

        // Act
        var actual = await dataset.ReadAsync<int[]>(selection);

        // Assert
        Assert.Equal<int[]>([2, 3, 17, 8, 21, 25, -1, -1], actual);
    }

    /// <summary>
    /// The same gather with every read of the file suspended.
    /// </summary>
    /// <remarks>
    /// This is what distinguishes a gather that awaits each source read from one that blocks on it.
    /// <para>
    /// What it does NOT show is a virtual dataset read entirely through the stream. The sources here are
    /// external files, resolved off the local filesystem and opened by path, so only the virtual
    /// dataset's own metadata comes through the suspending stream. <see cref="VirtualDatasetSameFileTests" />
    /// covers the same-file case, which is the one that works without a filesystem.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ReadAsyncOfAVirtualDatasetWorksWhenEveryReadSuspends()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.V110,
            fileId => TestUtils.AddVirtualDataset(fileId, "virtual"));

        using var stream = new ConcurrentStream(File.ReadAllBytes(filePath), suspend: true);
        using var root = await H5File.OpenAsync(stream, leaveOpen: true);
        var dataset = await root.DatasetAsync("vds");
        var selection = new HyperslabSelection(start: 3, stride: 4, count: 4, block: 2);

        // Act
        var actual = await dataset.ReadAsync<int[]>(selection);

        // Assert
        Assert.Equal<int[]>([2, 3, 17, 8, 21, 25, -1, -1], actual);
    }

    [Fact]
    public async Task ReadAsyncFillsACallerSuppliedBuffer()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, TestUtils.AddCompactDataset);

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = root.Dataset("compact/compact");

        var actual = new int[SharedTestData.SmallData.Length];

        // Act
        await dataset.ReadAsync(actual);

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.SmallData));
    }

    [Fact]
    public async Task ReadAsyncHonorsAFileSelection()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle: false));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = root.Group("chunked").Dataset("chunked_btree2");

        // The dataset is [n, 4] in chunks of [1000, 3], so this block straddles a boundary on both
        // axes and therefore spans four chunks - i.e. four awaited chunk reads, not one.
        var selection = new HyperslabSelection(
            rank: 2,
            starts: [995, 0],
            blocks: [10, 4]);

        // Act
        var actual = await dataset.ReadAsync<int[]>(selection);

        // Assert
        Assert.Equal(dataset.Read<int[]>(selection), actual);
        Assert.Equal(SharedTestData.MediumData.Skip(995 * 4).Take(10 * 4), actual);
    }

    /// <summary>
    /// The same reads against a stream whose every read completes asynchronously rather than inline,
    /// and which throws from every cursor-based <see cref="Stream" /> member.
    /// </summary>
    /// <remarks>
    /// This is the closest a test host can get to the situation the async surface exists for. A decode
    /// step that only worked because reads returned instantly - a chunk cache filled before its reader
    /// had finished, a stream position assumed to survive a suspension - fails here rather than in the
    /// tests above.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncWorksWhenEveryReadSuspends()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.LATEST, fileId =>
        {
            TestUtils.AddCompactDataset(fileId);
            TestUtils.AddContiguousDataset(fileId);
            TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle: false);
            TestUtils.AddFilteredDataset_ZLib(fileId);
        });

        try
        {
            using var stream = new ConcurrentStream(File.ReadAllBytes(filePath), suspend: true);
            using var root = H5File.Open(stream);

            // Act
            var compact = await root.Dataset("compact/compact").ReadAsync<int[]>();
            var contiguous = await root.Dataset("contiguous/contiguous").ReadAsync<int[]>();
            var chunked = await root.Group("chunked").Dataset("chunked_btree2").ReadAsync<int[]>();
            var filtered = await root.Group("filtered").Dataset("deflate").ReadAsync<int[]>();

            // Assert
            Assert.True(compact.SequenceEqual(SharedTestData.SmallData));
            Assert.True(contiguous.SequenceEqual(SharedTestData.HugeData));
            Assert.True(chunked.SequenceEqual(SharedTestData.MediumData));
            Assert.True(filtered.SequenceEqual(SharedTestData.MediumData));
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// A chunk cache that does not override <c>GetChunkAsync</c> must keep working through the
    /// interface's bridging default implementation - that is what makes the added member
    /// non-breaking for a third-party cache.
    /// </summary>
    [Fact]
    public async Task ReadAsyncWorksWithAChunkCacheThatOnlyImplementsTheSyncMember()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle: false));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var dataset = (NativeDataset)root.Group("chunked").Dataset("chunked_btree2");

        var cache = new SyncOnlyChunkCache();
        var datasetAccess = new H5DatasetAccess(ChunkCache: cache);

        // Act
        var actual = await dataset.ReadAsync<int[]>(datasetAccess);

        // Assert
        Assert.True(actual.SequenceEqual(SharedTestData.MediumData));
        Assert.True(cache.SyncCallCount > 0);
    }

    /// <summary>
    /// Implements only the required member, as a cache written against the previous version of the
    /// interface would.
    /// </summary>
    private sealed class SyncOnlyChunkCache : IReadingChunkCache
    {
        private readonly Dictionary<ulong, Memory<byte>> _chunks = [];

        public int SyncCallCount { get; private set; }

        public Memory<byte> GetChunk(ulong chunkIndex, Func<Memory<byte>> chunkReader)
        {
            SyncCallCount++;

            if (!_chunks.TryGetValue(chunkIndex, out var chunk))
            {
                chunk = chunkReader();
                _chunks[chunkIndex] = chunk;
            }

            return chunk;
        }
    }

    private static (Action<long> Build, string DatasetName) ChunkedCase(string indexForm, bool withShuffle)
    {
        return indexForm switch
        {
            "b-tree v1" => (
                fileId => TestUtils.AddChunkedDataset_Legacy(fileId, withShuffle),
                "chunked"),

            "b-tree v2" => (
                fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle),
                "chunked_btree2"),

            "fixed array" => (
                fileId => TestUtils.AddChunkedDataset_Fixed_Array(fileId, withShuffle),
                "chunked_fixed_array"),

            "fixed array, paged" => (
                fileId => TestUtils.AddChunkedDataset_Fixed_Array_Paged(fileId, withShuffle),
                "chunked_fixed_array_paged"),

            "extensible array, elements" => (
                fileId => TestUtils.AddChunkedDataset_Extensible_Array_Elements(fileId, withShuffle),
                "chunked_extensible_array_elements"),

            "extensible array, data blocks" => (
                fileId => TestUtils.AddChunkedDataset_Extensible_Array_Data_Blocks(fileId, withShuffle),
                "chunked_extensible_array_data_blocks"),

            "single chunk" => (
                fileId => TestUtils.AddChunkedDataset_Single_Chunk(fileId, withShuffle),
                "chunked_single_chunk"),

            _ => throw new Exception($"Unknown chunk index form '{indexForm}'.")
        };
    }
}

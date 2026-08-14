using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// The reader supports concurrent reads through one file - see <see cref="ConcurrencyTests" /> - but
/// two of its caches were mutated without synchronization while doing so.
/// </summary>
/// <remarks>
/// These are stress tests, and they detect a data race probabilistically rather than deterministically:
/// a corrupt dictionary is a consequence of unlucky interleaving, not of any particular call order. Both
/// failed reliably within a few runs against the unsynchronized code and pass consistently with it
/// fixed, which is the most a test of this shape can offer. What they do guarantee is the other
/// direction: neither can fail once the mutations are synchronized.
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class CacheConcurrencyTests
{
    /// <summary>
    /// A chunk cache shared across concurrent reads.
    /// </summary>
    /// <remarks>
    /// The default path never shares one - the default factory builds a cache per read - so this is
    /// reachable only by opting in through <see cref="H5DatasetAccess.ChunkCache" />, which is
    /// precisely why it went unnoticed. Sharing a cache is also the only reason to pass one: it is
    /// what makes repeated reads of the same chunks cheap.
    /// </remarks>
    [Fact]
    public void SharedChunkCacheSurvivesConcurrentReads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        using var root = NativeFile.InternalOpen(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            deleteOnClose: true);

        // The H5DatasetAccess overload of Read is on NativeDataset, not on the IH5Dataset interface.
        var dataset = (NativeDataset)root.Group("chunked").Dataset("chunked_huge");

        // Big enough to hold every chunk touched below, so that eviction is not what is under test.
        var sharedCache = new SimpleReadingChunkCache(chunkSlotCount: 512, byteCount: 64 * 1024 * 1024);
        var datasetAccess = new H5DatasetAccess(ChunkCache: sharedCache);

        const int CHUNK_SIZE = 1_000_000;

        // Act - overlapping ranges, so readers contend for the SAME cache entries rather than each
        // populating its own.
        Parallel.For(0, 20, i =>
        {
            var start = (uint)(i % 10) * CHUNK_SIZE;

            var fileSelection = new HyperslabSelection(
                start: start,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(datasetAccess, fileSelection);

            // Assert
            var expected = SharedTestData.HugeData.AsSpan((int)start, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(expected));
        });
    }

    /// <summary>
    /// Variable-length data resolves through the per-file global heap cache, which was a plain
    /// Dictionary behind a concurrent outer map - so the map that actually held the decoded collections
    /// was mutated unsynchronized on every miss.
    /// </summary>
    [Fact]
    public void ConcurrentVariableLengthReadsAreCorrect()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;

        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddString(fileId, ContainerType.Dataset));

        using var root = NativeFile.InternalOpen(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            deleteOnClose: true);

        var group = root.Group("string");
        var expected = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };

        // Act - every reader resolves out of the same global heap collections.
        Parallel.For(0, 64, _ =>
        {
            var actual = group.Dataset("variable").Read<string[]>();

            // Assert
            Assert.Equal(expected, actual);
        });
    }
}

using PureHDF.Filters;
using PureHDF.Selections;
using PureHDF.VOL.Native;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A chunked dataset written after <c>BeginWrite</c> holds no more of its data in memory than it has to.
/// </summary>
/// <remarks>
///     Buffering every chunk in the writing chunk cache until the writer is disposed would hold all of a
///     dataset's data, uncompressed, for the whole of the write, which defeats writing it in batches. An
///     unfiltered chunk has a fixed place in the file, so it is written there directly. A filtered chunk
///     has to be complete before it can be compressed, so it is buffered until its last element arrives and
///     written then.
/// </remarks>
public class ChunkedDeferredWriteTests
{
    private const int ChunkRows = 1000;

    [Fact]
    public void AnUnfilteredChunkIsWrittenStraightToTheFile()
    {
        // Arrange
        var cache = new RecordingChunkCache();
        var (dataset, data) = Column(rows: 4 * ChunkRows, cache);

        // Act
        var actual = WriteInBatches(dataset, data, batchRows: ChunkRows, filtered: false);

        // Assert
        Assert.Equal(data, actual);
        Assert.Equal(0, cache.ChunksRequested);
    }

    [Fact]
    public void AnUnfilteredSingleChunkWrittenInPiecesReadsBackWhole()
    {
        // Arrange - one chunk covering the dataset, whose address is only allocated on the first write
        var data = Enumerable.Range(0, ChunkRows).Select(i => (double)i).ToArray();

        var dataset = new H5Dataset<double[]>(
            fileDims: [(ulong)data.Length],
            chunks: [(uint)data.Length]);

        // Act
        var actual = WriteInBatches(dataset, data, batchRows: ChunkRows / 4, filtered: false);

        // Assert
        Assert.Equal(data, actual);
    }

    [Fact]
    public void AFilteredChunkIsWrittenAsSoonAsItIsComplete()
    {
        // Arrange
        var cache = new RecordingChunkCache();
        var (dataset, data) = Column(rows: 4 * ChunkRows, cache);

        // Act - batches aligned with chunks complete one chunk each
        var actual = WriteInBatches(dataset, data, batchRows: ChunkRows, filtered: true);

        // Assert
        Assert.Equal(data, actual);
        Assert.Equal(1, cache.MostHeldAtOnce);
    }

    [Fact]
    public void AFilteredChunkSpanningSeveralBatchesIsWrittenWhenItsLastPartArrives()
    {
        // Arrange
        var cache = new RecordingChunkCache();
        var (dataset, data) = Column(rows: 4 * ChunkRows + 300 /* a partial last chunk */, cache);

        // Act - batches that straddle chunk boundaries
        var actual = WriteInBatches(dataset, data, batchRows: 700, filtered: true);

        // Assert - at most the chunk being finished and the one being started
        Assert.Equal(data, actual);
        Assert.True(cache.MostHeldAtOnce <= 2, $"{cache.MostHeldAtOnce} chunks were held at once.");
    }

    [Fact]
    public void AFilteredTwoDimensionalBlockIsWrittenAsItsChunksComplete()
    {
        // Arrange - rows of a fixed width, written a block of rows at a time
        const int rows = 1024;
        const int width = 64;
        const int chunkRows = 256;

        var cache = new RecordingChunkCache();
        var data = new double[rows, width];

        for (var row = 0; row < rows; row++)
            for (var column = 0; column < width; column++)
                data[row, column] = row * width + column;

        var dataset = new H5Dataset<double[,]>(
            fileDims: [rows, width],
            chunks: [chunkRows, width],
            datasetCreation: new H5DatasetCreation(ChunkCache: cache));

        var file = new H5File { ["sweeps"] = dataset };
        var stream = new MemoryStream();

        // Act
        using (var writer = file.BeginWrite(stream, new H5WriteOptions(Filters: [DeflateFilter.Id])))
        {
            for (var start = 0; start < rows; start += chunkRows)
            {
                var block = new double[chunkRows, width];
                Buffer.BlockCopy(data, start * width * sizeof(double), block, 0, chunkRows * width * sizeof(double));

                writer.Write(dataset, block, fileSelection: new HyperslabSelection(
                    rank: 2,
                    starts: [(ulong)start, 0],
                    blocks: [chunkRows, width]));
            }
        }

        // Assert
        stream.Position = 0;
        using var readFile = H5File.Open(stream);

        Assert.Equal(data, readFile.Dataset("sweeps").Read<double[,]>());
        Assert.Equal(1, cache.MostHeldAtOnce);
    }

    [Fact]
    public void WritingAFilteredChunkAgainAfterItWasWrittenFails()
    {
        // Arrange
        var (dataset, data) = Column(rows: 2 * ChunkRows, chunkCache: null);
        var file = new H5File { ["column"] = dataset };

        using var writer = file.BeginWrite(new MemoryStream(), new H5WriteOptions(Filters: [DeflateFilter.Id]));
        writer.Write(dataset, data[..ChunkRows], fileSelection: new HyperslabSelection(0, ChunkRows));

        // Act / Assert - the chunk was complete, compressed and written; a later write cannot change it
        Assert.Throws<InvalidOperationException>(() =>
            writer.Write(dataset, data[..ChunkRows], fileSelection: new HyperslabSelection(0, ChunkRows)));
    }

    [Fact]
    public void AFilteredDatasetIsFinishedWithItsLastChunk()
    {
        // Arrange
        var (dataset, data) = Column(rows: 4 * ChunkRows, chunkCache: null);
        var file = new H5File { ["column"] = dataset };
        var stream = new MemoryStream();

        using (var writer = file.BeginWrite(stream, new H5WriteOptions(Filters: [DeflateFilter.Id])))
        {
            // Act
            writer.Write(dataset, data[..(3 * ChunkRows)], fileSelection: new HyperslabSelection(0, 3 * ChunkRows));
            var beforeLastChunk = stream.ToArray();

            writer.Write(dataset, data[(3 * ChunkRows)..], fileSelection: new HyperslabSelection(3 * ChunkRows, ChunkRows));
            var afterLastChunk = stream.ToArray();

            // Assert - the chunk index is written then, rather than at Dispose, and nothing of the
            // dataset's write state is kept
            Assert.Equal(-1, beforeLastChunk.AsSpan().IndexOf("FADB"u8));
            Assert.True(afterLastChunk.AsSpan().IndexOf("FADB"u8) >= 0);
            Assert.Null(writer.Context.Objects[dataset].Deferred!.H5D);
        }

        stream.Position = 0;
        using var actual = H5File.Open(stream);

        Assert.Equal(data, actual.Dataset("column").Read<double[]>());
    }

    [Fact]
    public void AFilteredSingleChunkDatasetIsFinishedWithItsChunk()
    {
        // Arrange
        var data = Enumerable.Range(0, ChunkRows).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset<double[]>(fileDims: [ChunkRows], chunks: [ChunkRows]);
        var file = new H5File { ["column"] = dataset };
        var stream = new MemoryStream();

        using (var writer = file.BeginWrite(stream, new H5WriteOptions(Filters: [DeflateFilter.Id])))
        {
            // Act
            writer.Write(dataset, data);

            // Assert
            Assert.Null(writer.Context.Objects[dataset].Deferred!.H5D);
            Assert.Empty(writer.Context.DatasetInfoToObjectHeaderMap);
        }

        stream.Position = 0;
        using var actual = H5File.Open(stream);

        Assert.Equal(data, actual.Dataset("column").Read<double[]>());
    }

    [Fact]
    public void AFilteredDatasetGivenWithTheTreeIsFinishedByBeginWrite()
    {
        // Arrange
        var data = Enumerable.Range(0, 4 * ChunkRows).Select(i => (double)i).ToArray();
        var dataset = new H5Dataset(data, chunks: [ChunkRows]);
        var file = new H5File { ["column"] = dataset };
        var stream = new MemoryStream();

        // Act
        using (var writer = file.BeginWrite(stream, new H5WriteOptions(Filters: [DeflateFilter.Id])))
        {
            // Assert
            Assert.Null(writer.Context.Objects[dataset].Deferred!.H5D);
        }

        stream.Position = 0;
        using var actual = H5File.Open(stream);

        Assert.Equal(data, actual.Dataset("column").Read<double[]>());
    }

    [Fact]
    public void WritingAFinishedDatasetAgainFails()
    {
        // Arrange
        var (dataset, data) = Column(rows: 2 * ChunkRows, chunkCache: null);
        var file = new H5File { ["column"] = dataset };

        using var writer = file.BeginWrite(new MemoryStream(), new H5WriteOptions(Filters: [DeflateFilter.Id]));
        writer.Write(dataset, data);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => writer.Write(dataset, data));
    }

    private static (H5Dataset<double[]> Dataset, double[] Data) Column(int rows, IWritingChunkCache? chunkCache)
    {
        var data = Enumerable.Range(0, rows).Select(i => (double)i).ToArray();

        var dataset = new H5Dataset<double[]>(
            fileDims: [(ulong)rows],
            chunks: [ChunkRows],
            datasetCreation: new H5DatasetCreation(ChunkCache: chunkCache));

        return (dataset, data);
    }

    private static double[] WriteInBatches(H5Dataset<double[]> dataset, double[] data, int batchRows, bool filtered)
    {
        var file = new H5File { ["column"] = dataset };
        var stream = new MemoryStream();
        var options = new H5WriteOptions(Filters: filtered ? [DeflateFilter.Id] : null);

        using (var writer = file.BeginWrite(stream, options))
        {
            for (var start = 0; start < data.Length; start += batchRows)
            {
                var count = Math.Min(batchRows, data.Length - start);

                writer.Write(dataset, data[start..(start + count)],
                    fileSelection: new HyperslabSelection((ulong)start, (ulong)count));
            }
        }

        stream.Position = 0;
        using var readFile = H5File.Open(stream);

        return readFile.Dataset("column").Read<double[]>();
    }

    /// <summary>
    ///     Records how many chunks the writer asks for, and the most it holds at once.
    /// </summary>
    private sealed class RecordingChunkCache : IWritingChunkCache
    {
        private readonly Dictionary<ulong, Memory<byte>> _chunks = new();

        public int ChunksRequested { get; private set; }

        public int MostHeldAtOnce { get; private set; }

        public Memory<byte> GetChunk(ulong chunkIndex, Func<Memory<byte>> chunkAllocator, Action<ulong, Memory<byte>> chunkWriter)
        {
            ChunksRequested++;

            if (!_chunks.TryGetValue(chunkIndex, out var chunk))
            {
                chunk = chunkAllocator();
                _chunks[chunkIndex] = chunk;
                MostHeldAtOnce = Math.Max(MostHeldAtOnce, _chunks.Count);
            }

            return chunk;
        }

        public bool TryRemove(ulong chunkIndex, out Memory<byte> chunk)
        {
            return _chunks.Remove(chunkIndex, out chunk);
        }

        public void Flush(Action<ulong, Memory<byte>> chunkWriter)
        {
            foreach (var (index, chunk) in _chunks)
                chunkWriter(index, chunk);

            _chunks.Clear();
        }
    }
}

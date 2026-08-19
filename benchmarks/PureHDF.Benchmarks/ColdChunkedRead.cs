using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.Selections;

namespace Benchmark;

// Exercises the COLD chunked-decode path: open a fresh file and read a
// hyperslab that spans several chunks, so the chunk index is walked and every
// touched chunk is decoded from scratch.
//
// WHY THIS EXISTS. The warm counterpart in MetadataRead (Warm_ReadChunkedSelection)
// warms the chunk cache in GlobalSetup, so every measured iteration is a chunk-
// cache hit and the per-chunk decode bridge at IReadingChunkCache.cs:38
// (chunkReader().GetAwaiter().GetResult()) never fires. That leaves the one
// hot sync-over-async bridge that is also the dominant real-world shape — a
// viewer opening a file and reading a chunked dataset for the first time —
// uncovered by any timing. A regression in the chunk-decode funnel, the chunk
// index walk, or read-ahead integration on chunk index blocks would pass
// undetected. This benchmark closes that gap.
//
// WHAT IT MEASURES, AND WHAT IT DOES NOT. As with MetadataRead.Cold, reads come
// from the OS page cache, so this is a syscall-plus-decode benchmark that
// understates the win on slower sources. Each iteration pays the full open
// cost (superblock, root group) plus the chunk index walk plus the per-chunk
// decode of the touched chunks; the open cost is itself guarded by
// Cold_OpenAndReadMetadata, so a chunk-decode-specific regression shows up as
// extra time on top of that baseline. The selection spans 10 chunks (rows
// 9800-10199 against a [40, 4] chunk grid) so chunk decode, not open,
// dominates the measured time.
//
// Measured on this machine (net10.0, default job). 5f0a23c is the last release
// before the driver read-ahead window; d972f97 adds it.
//
//   Method                    |  5f0a23c |  d972f97 | Speedup
//   --------------------------|---------:|---------:|--------:
//   Cold_ReadChunkedSelection | 104.9 us | 64.58 us |   1.62x
//
// d972f97 is 1.62x FASTER: the read-ahead window coalesces the chunk index blocks
// too, so the cold chunk-decode path benefits alongside the structural paths,
// and the sync-over-async bridge at IReadingChunkCache.cs:38 shows no
// regression. Allocations rose ~30% (27.9 -> 36.3 KB) - the window's memory
// cost, the same trade seen on Cold_OpenAndReadMetadata. Kept as the guard
// that this win does not regress.
[MemoryDiagnoser]
public class ColdChunkedRead
{
    private const int ChunkedRows = 20_000;
    private const int ChunkRows = 40;
    private const int Cols = 4;

    // Starts at row 9800 and spans 400 rows = 10 chunks of 40 rows each
    // (chunks 245..254), so the chunk index is walked and 10 chunks are
    // decoded from scratch on every cold iteration.
    private readonly HyperslabSelection _selection = new(
        rank: 2, starts: [9_800, 0], blocks: [400, Cols]);

    private string _filePath = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(
            Path.GetTempPath(),
            $"purehdf-cold-chunked-bench-{Guid.NewGuid():N}.h5");

        var chunkedData = new int[ChunkedRows * Cols];

        for (int i = 0; i < chunkedData.Length; i++)
        {
            chunkedData[i] = i;
        }

        var writeFile = new H5File
        {
            ["chunked"] = new H5Dataset(
                data: chunkedData,
                chunks: [ChunkRows, Cols],
                fileDims: [(ulong)ChunkedRows, Cols])
        };

        writeFile.Write(_filePath);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (File.Exists(_filePath))
        {
            try { File.Delete(_filePath); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Open, read a multi-chunk selection, close - the shape a viewer takes when
    /// it opens a file it has not opened before and reads a chunked dataset.
    /// </summary>
    /// <remarks>
    /// Disposing the file drops every structure cache AND the chunk cache, so each
    /// iteration decodes the superblock, the root object header, the chunk index
    /// and the touched chunk payloads from scratch.
    /// </remarks>
    [Benchmark]
    public int Cold_ReadChunkedSelection()
    {
        using var file = H5File.OpenRead(_filePath);
        var dataset = file.Dataset("chunked");
        return dataset.Read<int[]>(_selection).Length;
    }
}

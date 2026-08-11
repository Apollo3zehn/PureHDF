using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using PureHDF;
using PureHDF.Selections;

namespace Benchmark;

// Exercises the STRUCTURAL read path - object headers, link storage, attribute messages, chunk
// indexes - which is what the driver's read-ahead window (ReadAheadWindow) coalesces.
//
// WHY THIS EXISTS. No other benchmark in this project touches that path meaningfully.
// ReflectionDispatch reads a scalar from an already-resolved contiguous dataset, so it decodes almost
// no structure and its payload read bypasses the window entirely; the read-ahead change moved it by
// ~1%, which is under this machine's noise floor. The read counts in NavigationCostTests are the
// precise instrument (they are deterministic where a timing is not), but they cannot say what the
// coalescing is worth in TIME, and on a local file the thing being coalesced is a pread. That is what
// this measures.
//
// WHAT IT MEASURES, AND WHAT IT DOES NOT. Reads come from the OS page cache, not from a disk - the
// file is written and then read repeatedly, so it is resident throughout. So this is a syscall-plus-
// decode benchmark, and it UNDERSTATES the win for any source where a read is more expensive than a
// pread: a network file system, or the HTTP range-request stream this work was motivated by. It does
// not overstate it.
//
// The file is written with PureHDF's own writer so that this project needs no HDF.PInvoke dependency.
// That has a consequence which turned out to matter: the writer stores attributes COMPACTLY, in the
// object header, and a compact attribute is retained already decoded as part of the cached
// ObjectHeader - so enumerating them performs no reads at all, and Warm_EnumerateAttributes measures
// decode bookkeeping rather than I/O. The pathological attribute shape is DENSE storage (fractal heap
// plus b-tree v2), which needs HDF.PInvoke to produce and is covered by read counts instead, in
// NavigationCostTests: 363,269 reads to enumerate 1000 dense attributes before the window, 1,042
// after.
//
// MEASURED on this machine (net10.0, Release), baseline = the commit before the read-ahead window.
// Allocation is identical everywhere except the cold case, where it grows by ~8 KB: two 4 KiB windows,
// one for the file-level driver and one for the per-operation driver.
//
//   Cold_OpenAndReadMetadata     22,102 us -> 2,405 us    9.2x faster
//   Warm_LookupLinksByName      356,136 us -> 27,098 us   13.1x faster
//   Warm_EnumerateAttributes          9.45 us -> 10.00 us  5.8% slower
//   Warm_ReadChunkedSelection         3.55 us ->  3.73 us  5.0% slower
//
// The two small regressions are the honest cost of the window and are worth keeping visible: both
// benchmarks re-read data that is ALREADY decoded and cached, so they get no coalescing benefit and
// pay the window's extra bounds check and copy. At single-digit microseconds a few hundred nanoseconds
// reads as several percent. Nothing at this scale offsets a 9-13x win on the paths that do touch the
// file, but a change that made these materially worse would be a real regression.
[MemoryDiagnoser]
public class MetadataRead
{
    // A compound with a nested member, so that the datatype message carries a realistic number of
    // member names. Names are decoded one byte at a time - 272 of the 484 reads in the dense-attribute
    // measurement were exactly that - so member count drives the cost more than payload size does.
    [StructLayout(LayoutKind.Sequential)]
    public struct Reading
    {
        public double Wavelength;
        public double Power;
        public float Temperature;
        public int ChannelIndex;
        public long TimestampTicks;
        public Coordinates Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Coordinates
    {
        public int Row;
        public int Column;
        public int Die;
    }

    private const int LinkCount = 1_000;
    private const int AttributeCount = 200;
    private const int ChunkedRows = 20_000;

    private string _filePath = default!;

    // Held open for the warm benchmarks. The cold benchmark opens its own.
    private PureHDF.VOL.Native.NativeFile _file = default!;
    private IH5Group _links = default!;
    private IH5Dataset _annotated = default!;
    private IH5Dataset _chunked = default!;

    // Spans several chunks on both axes, so the chunk index is walked rather than hit once.
    private readonly HyperslabSelection _selection = new(rank: 2, starts: [9_995, 0], blocks: [20, 4]);

    [GlobalSetup]
    public void GlobalSetup()
    {
        _filePath = Path.Combine(
            Path.GetTempPath(),
            $"purehdf-metadata-bench-{Guid.NewGuid():N}.h5");

        var links = new H5Group();

        for (int i = 0; i < LinkCount; i++)
        {
            links[$"member_{i:D4}"] = new H5Group();
        }

        var annotated = new H5Dataset(data: 0);

        for (int i = 0; i < AttributeCount; i++)
        {
            annotated.Attributes[$"reading_{i:D4}"] = new Reading
            {
                Wavelength = 1550.0 + i,
                Power = -3.5,
                Temperature = 25.0f,
                ChannelIndex = i,
                TimestampTicks = i * 1_000L,
                Position = new Coordinates { Row = i, Column = i * 2, Die = i * 3 }
            };
        }

        var chunkedData = new int[ChunkedRows * 4];

        for (int i = 0; i < chunkedData.Length; i++)
        {
            chunkedData[i] = i;
        }

        var writeFile = new H5File
        {
            ["links"] = links,
            ["annotated"] = annotated,
            ["chunked"] = new H5Dataset(
                data: chunkedData,
                chunks: [40, 4],
                fileDims: [(ulong)ChunkedRows, 4])
        };

        writeFile.Write(_filePath);

        _file = H5File.OpenRead(_filePath);
        _links = _file.Group("links");
        _annotated = _file.Dataset("annotated");
        _chunked = _file.Dataset("chunked");

        // Warm the structure caches, so the warm benchmarks measure the marginal cost of navigating
        // again rather than the one-off decode of the storage they walk.
        _ = _links.Children().ToList();
        _ = _annotated.Attributes().ToList();
        _ = _chunked.Read<int[]>(_selection);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _file?.Dispose();

        if (File.Exists(_filePath))
        {
            try { File.Delete(_filePath); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Open, read the metadata of interest, close - the shape a viewer takes when it shows a file it
    /// has not opened before.
    /// </summary>
    /// <remarks>
    /// The most representative of these four. Disposing the file drops every structure cache, so each
    /// iteration decodes the superblock, the root object header, the link storage and the attribute
    /// messages from scratch - which is precisely the case a cache cannot help with and coalescing
    /// can.
    /// </remarks>
    [Benchmark]
    public int Cold_OpenAndReadMetadata()
    {
        using var file = H5File.OpenRead(_filePath);

        var count = file.Group("links").Children().Count();

        foreach (var attribute in file.Dataset("annotated").Attributes())
        {
            count += attribute.Name.Length;
        }

        return count;
    }

    /// <summary>
    /// Enumerating attributes on a file already open.
    /// </summary>
    /// <remarks>
    /// This was intended to measure the field-by-field attribute decode and does not: the writer
    /// stores these attributes compactly, so they are already decoded inside the cached ObjectHeader
    /// and this reads nothing from the file. Kept anyway, as the regression guard for a path that gets
    /// no benefit from the window and therefore only pays for it.
    /// </remarks>
    [Benchmark]
    public int Warm_EnumerateAttributes()
    {
        var count = 0;

        foreach (var attribute in _annotated.Attributes())
        {
            count += attribute.Name.Length;
        }

        return count;
    }

    /// <summary>
    /// Repeated by-name link lookups, each of which walks the group's link storage.
    /// </summary>
    [Benchmark]
    public int Warm_LookupLinksByName()
    {
        var count = 0;

        for (int i = 0; i < 100; i++)
        {
            count += _links.Group($"member_{i * 10:D4}").Name.Length;
        }

        return count;
    }

    /// <summary>
    /// A hyperslab spanning several chunks, so the chunk index is walked on every read.
    /// </summary>
    [Benchmark]
    public int Warm_ReadChunkedSelection()
    {
        return _chunked.Read<int[]>(_selection).Length;
    }
}

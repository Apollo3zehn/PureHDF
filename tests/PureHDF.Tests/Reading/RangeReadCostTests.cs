using System.Diagnostics;
using PureHDF.Filters;
using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// What it costs a remote client to open a file - round trips and bytes - and how much of that cost is
/// decided by where the writer put the structure.
/// </summary>
/// <remarks>
/// The read path is asynchronous, so a file can be range-read over a network transport - but whether that
/// is practical depends on what a full structure walk actually pulls over the wire, which is a property of
/// how the file was written rather than of the reader.
/// <para>
/// Attribute VALUES are deliberately not read. PureHDF stores attributes compactly, so an attribute's
/// value sits in the same object header as its name and datatype - already in a block the walk fetched -
/// and reading it costs no additional request. The exception is a variable-length value, which lives in
/// the global heap; the numeric attributes used here have none, so the walk below is the whole
/// metadata cost rather than most of it.
/// </para>
/// </remarks>
public class RangeReadCostTests(ITestOutputHelper output)
{
    /// <summary>
    /// Block size for the controlled comparison. Smaller than the 1 MiB a real range client typically
    /// fetches, because a synthetic file small enough to write in a unit test spans only a handful of
    /// 1 MiB blocks - too few for a difference in locality to be expressible at all.
    /// </summary>
    private const long ControlBlockSize = 256 * 1024;

    private const int GroupCount = 600;

    /// <param name="MaxDatasetBytes">
    ///     Largest single dataset, uncompressed, from its shape and type alone - an upper bound on what a
    ///     client must fetch to read one dataset whole, since a filtered chunk cannot be partially
    ///     decompressed and so even a strided read pulls every chunk it spans.
    /// </param>
    private sealed record WalkResult(
        int Groups,
        int Datasets,
        int Attributes,
        long MaxDatasetBytes,
        long TotalDatasetBytes);

    /// <summary>
    /// Builds a tree shaped like a report: many small groups, each carrying attributes and a dataset.
    /// </summary>
    /// <remarks>
    /// The payload is a periodic signal with a slow drift, rounded to two decimals - a measurement
    /// series, which is what such files typically hold. Its shape is chosen for how it compresses, since the file
    /// sizes reported below are only as meaningful as the payload is representative. Three properties
    /// matter, and a plainer choice fails at least one:
    /// <list type="bullet">
    /// <item>It compresses, to about 39%, so the file is neither dominated by payload nor unrealistically
    /// small - a run of zeroes compresses away to nothing and leaves too few blocks for locality to be
    /// expressible at all.</item>
    /// <item>It compresses to nearly the same size on every runtime. .NET 9 replaced the bundled zlib
    /// with zlib-ng, whose match finder differs below maximum effort, so a payload can compress quite
    /// differently across target frameworks: this one lands within 0.2%, where a strided integer ramp -
    /// pathological for a fast match finder - differed by 73% and moved every figure here with it.</item>
    /// <item>It is deterministic, so the figures are reproducible rather than merely typical.</item>
    /// </list>
    /// The rounding is what buys the compression: full-mantissa doubles differ in their low bytes at
    /// every sample and barely compress at all.
    /// </remarks>
    private static H5File BuildTree()
    {
        var root = new H5File();

        for (var i = 0; i < GroupCount; i++)
        {
            // 2,048 doubles is 16 kB of payload per group, which keeps the file the size the block
            // arithmetic above assumes.
            var data = new double[2_048];

            for (var j = 0; j < data.Length; j++)
            {
                data[j] = Math.Round(Math.Sin((j + i) / 50.0) * 100 + (j * 0.01), 2);
            }

            root[$"unit{i:D4}"] = new H5Group
            {
                Attributes = new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["scale"] = i * 1.5,
                },
                ["values"] = new H5Dataset(data)
            };
        }

        return root;
    }

    /// <summary>
    /// Walks every group, dataset and attribute, touching each dataset's shape and type - the same
    /// metadata-only traversal a viewer performs to build its tree and decide what is plottable.
    /// </summary>
    private static async Task<WalkResult> WalkAsync(IH5Group group)
    {
        var groups = 1;
        var datasets = 0;
        var attributes = (await group.AttributesAsync()).Count();
        var maxDatasetBytes = 0L;
        var totalDatasetBytes = 0L;

        foreach (var child in await group.ChildrenAsync())
        {
            switch (child)
            {
                case IH5Group childGroup:
                    var nested = await WalkAsync(childGroup);
                    groups += nested.Groups;
                    datasets += nested.Datasets;
                    attributes += nested.Attributes;
                    maxDatasetBytes = Math.Max(maxDatasetBytes, nested.MaxDatasetBytes);
                    totalDatasetBytes += nested.TotalDatasetBytes;
                    break;

                case IH5Dataset dataset:
                    datasets++;

                    var elements = dataset.Space.Dimensions.Aggregate(1UL, (product, length) => product * length);
                    var bytes = (long)elements * dataset.Type.Size;

                    maxDatasetBytes = Math.Max(maxDatasetBytes, bytes);
                    totalDatasetBytes += bytes;

                    attributes += (await dataset.AttributesAsync()).Count();
                    break;
            }
        }

        return new WalkResult(groups, datasets, attributes, maxDatasetBytes, totalDatasetBytes);
    }

    private async Task<(WalkResult Walk, int Requests, long Bytes, int Blocks, long Milliseconds)> MeasureWalkAsync(
        string filePath,
        long blockSize)
    {
        using var stream = new RangeRequestStream(filePath, blockSize);

        var stopwatch = Stopwatch.StartNew();

        using var file = await H5File.OpenAsync(stream, leaveOpen: true);

        var walk = await WalkAsync(file);

        stopwatch.Stop();

        return (walk, stream.Requests, stream.BytesFetched, stream.BlocksResident, stopwatch.ElapsedMilliseconds);
    }

    private void Report(string label, string filePath, (WalkResult Walk, int Requests, long Bytes, int Blocks, long Milliseconds) result)
    {
        var size = new FileInfo(filePath).Length;

        output.WriteLine(
            $"{label,-14} file {size,13:N0} B | fetched {result.Bytes,13:N0} B "
            + $"({(double)result.Bytes / size,6:P1} of file) | {result.Requests,6:N0} requests "
            + $"| {result.Blocks,5:N0} blocks resident | {result.Milliseconds,6:N0} ms "
            + $"| {result.Walk.Groups:N0} groups, {result.Walk.Datasets:N0} datasets, {result.Walk.Attributes:N0} attributes");
    }

    /// <summary>
    /// The same content written every way, so the only variable is where the structure went.
    /// </summary>
    /// <remarks>
    /// Both clustered placements are measured, not just the best one. They answer different questions: a
    /// reservation is sized by measuring the file first and gets the structure into one range, while
    /// aggregation needs no such pass and settles for a handful. A caller choosing between them wants the
    /// gap between the two, and a caller who cannot afford the sizing pass wants to know that aggregation
    /// is still worth having - neither of which a two-way comparison against interleaving shows.
    /// </remarks>
    [Fact]
    public async Task EveryClusteredPlacementIsFarCheaperToWalkRemotelyThanInterleaving()
    {
        // Arrange - deflate forces chunked layout, without which these datasets would be stored
        // compact (payload inside the object header) and there would be no separation to measure.
        var interleavedPath = Path.GetTempFileName();
        var aggregatedPath = Path.GetTempFileName();
        var frontLoadedPath = Path.GetTempFileName();

        try
        {
            void Write(string filePath, H5MetadataPlacement placement) => BuildTree().Write(
                filePath,
                new H5WriteOptions(Filters: [DeflateFilter.Id]) { MetadataPlacement = placement });

            Write(interleavedPath, H5MetadataPlacement.Interleaved);
            Write(aggregatedPath, H5MetadataPlacement.Aggregated);
            Write(frontLoadedPath, H5MetadataPlacement.FrontLoaded);

            // Act
            var interleaved = await MeasureWalkAsync(interleavedPath, ControlBlockSize);
            var aggregated = await MeasureWalkAsync(aggregatedPath, ControlBlockSize);
            var frontLoaded = await MeasureWalkAsync(frontLoadedPath, ControlBlockSize);

            Report("interleaved", interleavedPath, interleaved);
            Report("aggregated", aggregatedPath, aggregated);
            Report("front-loaded", frontLoadedPath, frontLoaded);

            // Assert - the walk must see the same file every way, or the comparison is meaningless.
            Assert.Equal(interleaved.Walk, aggregated.Walk);
            Assert.Equal(interleaved.Walk, frontLoaded.Walk);
            Assert.Equal(GroupCount, frontLoaded.Walk.Datasets);

            // The file has to be big enough in blocks for locality to be expressible at all.
            var blocksInFile = (new FileInfo(interleavedPath).Length + ControlBlockSize - 1) / ControlBlockSize;
            Assert.True(blocksInFile > 8, $"the file spans only {blocksInFile} blocks, too few to measure locality");

            Assert.True(
                frontLoaded.Bytes < interleaved.Bytes / 2,
                $"front-loading must at least halve what a remote walk transfers, but it fetched "
                + $"{frontLoaded.Bytes:N0} B against {interleaved.Bytes:N0} B");

            Assert.True(
                frontLoaded.Blocks < interleaved.Blocks,
                $"a front-loaded walk must hold fewer blocks, but it held {frontLoaded.Blocks} against "
                + $"{interleaved.Blocks}");

            // Aggregation has to earn its place without a sizing pass.
            Assert.True(
                aggregated.Bytes < interleaved.Bytes / 2,
                $"aggregation must at least halve what a remote walk transfers, but it fetched "
                + $"{aggregated.Bytes:N0} B against {interleaved.Bytes:N0} B");

            // Measuring the file cannot do worse than not measuring it.
            Assert.True(
                frontLoaded.Bytes <= aggregated.Bytes,
                $"a measured reservation fetched {frontLoaded.Bytes:N0} B against aggregation's "
                + $"{aggregated.Bytes:N0} B, so measuring the file bought nothing");

            // Round trips, which is what the whole exercise is about and what every figure quoted for
            // this feature is stated in. A measured reservation is one contiguous span, so walking the
            // structure is one request; asserted as a number rather than a ratio because the number is
            // the claim. Aggregation clusters into a handful, and interleaving needs one per block it
            // spans.
            Assert.Equal(1, frontLoaded.Requests);

            Assert.True(
                aggregated.Requests > 1 && aggregated.Requests * 4 < interleaved.Requests,
                $"aggregation took {aggregated.Requests} requests against interleaving's "
                + $"{interleaved.Requests}, which is not the handful-against-all-of-them this reports");

            // What aggregation must not do is buy that locality with file size. Blocks are claimed in
            // full, so a block of fixed size is a floor rather than a proportional cost: at a fixed 8 MB
            // this file would come out three times larger, which is a bad trade however few ranges it
            // takes to walk. Growth is bounded from the reader's side here and from the writer's side in
            // MetadataLayoutTests.
            var interleavedSize = new FileInfo(interleavedPath).Length;
            var aggregatedSize = new FileInfo(aggregatedPath).Length;

            Assert.True(
                aggregatedSize < interleavedSize * 1.1,
                $"aggregation produced {aggregatedSize:N0} bytes against {interleavedSize:N0} interleaved, "
                + $"so a whole block is being claimed regardless of how little metadata the file has");
        }

        finally
        {
            File.Delete(interleavedPath);
            File.Delete(aggregatedPath);
            File.Delete(frontLoadedPath);
        }
    }
}

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
    private static H5File BuildTree()
    {
        var root = new H5File();

        for (var i = 0; i < GroupCount; i++)
        {
            var data = new int[4_096];

            for (var j = 0; j < data.Length; j++)
            {
                // Varied, because deflate over a run of zeroes compresses unrepresentatively well and
                // would leave the file too small for block-level effects to show.
                data[j] = (i * 31) + (j * 7);
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
    /// The same content written both ways, so the only variable is where the structure went.
    /// </summary>
    [Fact]
    public async Task AFrontLoadedFileIsFarCheaperToWalkRemotelyThanAnInterleavedOne()
    {
        // Arrange - deflate forces chunked layout, without which these datasets would be stored
        // compact (payload inside the object header) and there would be no separation to measure.
        var interleavedPath = Path.GetTempFileName();
        var frontLoadedPath = Path.GetTempFileName();

        try
        {
            BuildTree().Write(
                interleavedPath,
                new H5WriteOptions(Filters: [DeflateFilter.Id]) { MetadataPlacement = H5MetadataPlacement.Interleaved });

            BuildTree().Write(
                frontLoadedPath,
                new H5WriteOptions(Filters: [DeflateFilter.Id]) { MetadataPlacement = H5MetadataPlacement.FrontLoaded });

            // Act
            var interleaved = await MeasureWalkAsync(interleavedPath, ControlBlockSize);
            var frontLoaded = await MeasureWalkAsync(frontLoadedPath, ControlBlockSize);

            Report("interleaved", interleavedPath, interleaved);
            Report("front-loaded", frontLoadedPath, frontLoaded);

            // Assert - the walk must see the same file both ways, or the comparison is meaningless.
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
        }

        finally
        {
            File.Delete(interleavedPath);
            File.Delete(frontLoadedPath);
        }
    }
}

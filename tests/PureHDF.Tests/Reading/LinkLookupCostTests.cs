using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Guards that a by-name lookup does not re-decode the object header of the group it is called on.
/// </summary>
/// <remarks>
/// <c>TryWalkPath</c> used to begin by dereferencing the group's OWN reference, which builds a second
/// <c>NativeGroup</c> and decodes that group's object header again from the file. The cost is
/// proportional to the number of LINKS rather than to the depth of the path, because a group storing
/// its links compactly holds one header message per link - so a single lookup in a 1000-link group
/// re-read 30,113 bytes and allocated 2.1 MB, and a missing name cost exactly as much as a hit.
/// <para>
/// EVERY LOOKUP HERE USES A DIFFERENT NAME, and that is the point. The driver's read-ahead window is
/// 4 KiB, so it covers a small group's whole object header and would hide this entirely: measured
/// against the unfixed code, a 100-link group reported ZERO bytes read while a 1000-link group reported
/// 30 KB. Repeating one name would look clean for the same reason. So the group here is large enough
/// that its header cannot fit in the window, and the names vary.
/// </para>
/// <para>
/// Written with PureHDF's own writer deliberately. It emits no b-tree name index, so links are always
/// stored compactly no matter how many there are - which is the shape that makes this expensive, and
/// the shape a file produced by this library always has. See notes/backlog.md.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class LinkLookupCostTests
{
    private const int LinkCount = 1_000;

    private readonly ITestOutputHelper _output;

    public LinkLookupCostTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RepeatedLookupsDoNotRedecodeTheGroupHeader()
    {
        // Arrange
        var fileBytes = WriteGroupWithManyLinks();

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("links");

        // Warm up: decodes the group's object header, including all LinkCount link messages.
        _ = group.LinkExists("member_0000");

        // Act - 20 DISTINCT names, spread across the group so that no two are adjacent.
        stream.ResetCounts();

        for (int i = 0; i < 20; i++)
        {
            Assert.True(group.LinkExists($"member_{i * 47:D4}"));
        }

        var bytes = stream.MetadataBytesRead;
        var reads = stream.MetadataReadCount;

        _output.WriteLine($"20 lookups of distinct names: {reads} reads, {bytes} bytes");

        // Assert - the header is already held, so resolving a name out of it should read nothing at
        // all. Asserting zero rather than a bound: there is no structure left to fetch, and a bound
        // would quietly tolerate the regression coming back at a smaller size.
        Assert.Equal(0, reads);
        Assert.Equal(0, bytes);
    }

    /// <summary>
    /// The cost of a lookup must not grow with the number of links in the group.
    /// </summary>
    /// <remarks>
    /// The scaling check is what identifies this defect specifically, as opposed to any other reason a
    /// lookup might read: the re-decode was linear in the link count, so doubling the links doubled the
    /// bytes read. A residual per-lookup cost that is FLAT in the link count would be something else.
    /// </remarks>
    [Fact]
    public void LookupCostDoesNotGrowWithLinkCount()
    {
        var measurements = new List<(int Links, long Bytes)>();

        foreach (var linkCount in new[] { 500, 1_000, 2_000 })
        {
            var fileBytes = WriteGroupWithManyLinks(linkCount);

            using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
            using var root = H5File.Open(stream, leaveOpen: true);
            var group = root.Group("links");

            _ = group.LinkExists("member_0000");

            stream.ResetCounts();
            _ = group.LinkExists($"member_{linkCount / 2:D4}");

            measurements.Add((linkCount, stream.MetadataBytesRead));
        }

        foreach (var (links, bytes) in measurements)
        {
            _output.WriteLine($"{links,5} links: {bytes,8:N0} bytes per lookup");
        }

        Assert.All(measurements, measurement => Assert.Equal(0, measurement.Bytes));
    }

    /// <summary>
    /// The walk must still work for the cases that genuinely need a dereference: a rooted path, and the
    /// intermediate segments of a nested one.
    /// </summary>
    /// <remarks>
    /// The fix reuses <c>this</c> for the FIRST segment of a relative path only, so these are the paths
    /// where it must not have broken anything - and the nested case additionally proves that `group` is
    /// cleared between segments, since reusing it would resolve every segment against the same group and
    /// find nothing.
    /// </remarks>
    [Fact]
    public void RootedAndNestedPathsStillResolve()
    {
        // Arrange
        var filePath = Path.GetTempFileName();

        try
        {
            var leaf = new H5Group { ["target"] = new H5Dataset(data: 42) };
            var middle = new H5Group { ["leaf"] = leaf };

            new H5File { ["outer"] = new H5Group { ["middle"] = middle } }.Write(filePath);

            using var root = H5File.OpenRead(filePath);

            // Act + Assert - relative, nested, from the file.
            Assert.True(root.LinkExists("outer/middle/leaf/target"));
            Assert.Equal(42, root.Dataset("outer/middle/leaf/target").Read<int>());

            // ... rooted, from the file.
            Assert.True(root.LinkExists("/outer/middle/leaf/target"));

            // ... relative and rooted, from a group partway down. The rooted lookup must resolve from
            // the FILE, not from the group it was called on.
            var outer = root.Group("outer");

            Assert.True(outer.LinkExists("middle/leaf/target"));
            Assert.True(outer.LinkExists("/outer/middle/leaf/target"));
            Assert.False(outer.LinkExists("/middle/leaf/target"));

            // A name that does not exist must still report not-found rather than throwing.
            Assert.False(outer.LinkExists("nope"));
            Assert.False(root.LinkExists("outer/nope/leaf"));

            // Get takes the same walk and must agree with LinkExists about what resolves.
            Assert.Throws<Exception>(() => outer.Get("nope"));
            Assert.NotNull(outer.Get("middle/leaf/target"));
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static byte[] WriteGroupWithManyLinks(int linkCount = LinkCount)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var links = new H5Group();

            for (int i = 0; i < linkCount; i++)
            {
                links[$"member_{i:D4}"] = new H5Group();
            }

            new H5File { ["links"] = links }.Write(filePath);

            return File.ReadAllBytes(filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}

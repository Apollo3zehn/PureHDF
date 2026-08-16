using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Guards that a by-name lookup does not re-decode the object header of the group it is called on.
/// </summary>
/// <remarks>
/// The walk used to begin by dereferencing the group's OWN reference, which builds a second
/// <c>NativeGroup</c> and decodes that group's object header again from the file. The cost is
/// proportional to the number of LINKS rather than to the depth of the path, because a group storing
/// its links compactly holds one header message per link - so a single lookup in a 1000-link group
/// re-read tens of kilobytes, and a name that did not exist cost exactly as much as one that did.
/// <para>
/// Every lookup here uses a DIFFERENT name, deliberately: repeating one name would be served from the
/// decoded header either way and would look clean even with the defect present.
/// </para>
/// <para>
/// Written with PureHDF's own writer, also deliberately. It emits no b-tree name index, so links are
/// always stored compactly no matter how many there are - which is both the shape that makes this
/// expensive and the shape a file produced by this library always has.
/// </para>
/// </remarks>
public class LinkLookupCostTests(ITestOutputHelper output)
{
    /// <summary>
    /// Counts what the reader actually pulls from the stream, so a lookup's cost can be asserted
    /// rather than inferred.
    /// </summary>
    private sealed class CountingStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public long BytesRead { get; private set; }

        public int ReadCount { get; private set; }

        public void ResetCounts()
        {
            BytesRead = 0;
            ReadCount = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);

            BytesRead += read;
            ReadCount++;

            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);

            BytesRead += read;
            ReadCount++;

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void RepeatedLookupsDoNotRedecodeTheGroupHeader()
    {
        // Arrange
        using var stream = new CountingStream(WriteGroupWithManyLinks(1_000));
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("links");

        // Warm up: decodes the group's object header, including all of its link messages.
        _ = group.LinkExists("member_0000");

        // Act - 20 distinct names, spread across the group so that no two are adjacent.
        stream.ResetCounts();

        for (int i = 0; i < 20; i++)
        {
            Assert.True(group.LinkExists($"member_{i * 47:D4}"));
        }

        output.WriteLine($"20 lookups of distinct names: {stream.ReadCount} reads, {stream.BytesRead} bytes");

        // Assert - the header is already held, so resolving a name out of it should read nothing at
        // all. Asserting zero rather than a bound: there is no structure left to fetch, and a bound
        // would quietly tolerate the regression coming back at a smaller size.
        Assert.Equal(0, stream.ReadCount);
        Assert.Equal(0, stream.BytesRead);
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
            using var stream = new CountingStream(WriteGroupWithManyLinks(linkCount));
            using var root = H5File.Open(stream, leaveOpen: true);
            var group = root.Group("links");

            _ = group.LinkExists("member_0000");

            stream.ResetCounts();
            _ = group.LinkExists($"member_{linkCount / 2:D4}");

            measurements.Add((linkCount, stream.BytesRead));
        }

        foreach (var (links, bytes) in measurements)
        {
            output.WriteLine($"{links,5} links: {bytes,8:N0} bytes per lookup");
        }

        Assert.All(measurements, measurement => Assert.Equal(0, measurement.Bytes));
    }

    /// <summary>
    /// The walk must still work for the cases that genuinely need a dereference: a rooted path, and the
    /// intermediate segments of a nested one.
    /// </summary>
    /// <remarks>
    /// The fix reuses <c>this</c> for the FIRST segment of a relative path only, so these are the paths
    /// where it must not have broken anything - and the nested case additionally proves the reuse is
    /// cleared between segments, since keeping it would resolve every segment against the same group
    /// and find nothing.
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

    private static byte[] WriteGroupWithManyLinks(int linkCount)
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

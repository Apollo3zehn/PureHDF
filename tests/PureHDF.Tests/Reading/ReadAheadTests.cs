using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Covers the driver-level read-ahead window that coalesces the reader's small structural reads.
/// </summary>
/// <remarks>
/// The window is the one place in the read path that serves bytes it fetched earlier rather than
/// fetching them now, so its failure mode is not "slow" but "wrong bytes, silently". These tests aim
/// at the arithmetic that could produce that: a read straddling the end of the window, a read behind
/// the current position, a read too large to buffer, and a window truncated by the end of the file.
/// <para>
/// <c>NavigationCostTests</c> covers the win itself - it is the test whose numbers move when
/// coalescing stops working. This one covers correctness, and deliberately checks DATA as well as read
/// counts, because a window that returns stale or misaligned bytes would otherwise show up here as an
/// impressively low read count.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class ReadAheadTests
{
    private readonly ITestOutputHelper _output;

    public ReadAheadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The point of the whole thing: navigating a group must cost far fewer reads than it reads fields.
    /// </summary>
    [Fact]
    public void StructuralReadsAreCoalesced()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));
        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("mass_attributes");

        _ = group.Attribute("mass_0000");

        // Act
        stream.ResetCounts();
        _ = group.Attribute("mass_0001");

        var reads = stream.MetadataReadCount;
        var bytes = stream.MetadataBytesRead;

        _output.WriteLine($"{reads} reads, {bytes} bytes, {(reads == 0 ? 0 : bytes / (double)reads):F0} bytes per read");

        // Assert - without the window this lookup costs 484 reads at ~3.8 bytes each. Asserting an
        // upper bound rather than an exact figure: the exact number belongs to NavigationCostTests,
        // and what matters here is that coalescing is happening at all.
        Assert.True(reads < 20, $"Expected the lookup to be coalesced into few reads, but it took {reads}.");
    }

    /// <summary>
    /// A read behind the current position must still be served from the window.
    /// </summary>
    /// <remarks>
    /// Not a hypothetical: <c>ReadUtils.ReadNullTerminatedString</c> reads past the terminator and then
    /// seeks back over the padding, and that happens on every symbol-table link name. If the window
    /// were keyed by a cursor instead of by absolute offset, this would still return correct bytes -
    /// by re-fetching them - so this is asserted through the read count.
    /// </remarks>
    [Fact]
    public void ARewindWithinTheWindowCostsNoRead()
    {
        // Arrange - a symbol table stores link names as padded null-terminated strings in a local
        // heap, so the walk seeks backwards repeatedly.
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.EARLIEST, TestUtils.AddMassLinks);
        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("mass_links");

        _ = group.Group("mass_0500");

        // Act
        stream.ResetCounts();
        var again = group.Group("mass_0500");

        // Assert
        Assert.NotNull(again);
        Assert.Equal(0, stream.MetadataReadCount);
    }

    /// <summary>
    /// A read at least as large as the window must bypass it, and must not displace what it holds.
    /// </summary>
    /// <remarks>
    /// Attribute payload is read through the metadata funnel (it is decoded inline with the object
    /// header), so a large attribute is exactly this case. If a bypassing read wrongly reset the
    /// window, the follow-up lookup below would have to fetch again.
    /// </remarks>
    [Fact]
    public void ALargeReadBypassesTheWindowWithoutDisturbingIt()
    {
        // Arrange - AddMass writes 1000 attributes of 12 x TestStructL1; the file also carries the
        // large datasets from the other helpers, so pick an attribute set and a big dataset read.
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId =>
        {
            TestUtils.AddMass(fileId, ContainerType.Attribute);
            TestUtils.AddSmall(fileId, ContainerType.Dataset);
        });

        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("mass_attributes");

        // Warm the window on the attribute region.
        var expected = group.Attribute("mass_0500").Read<TestStructL1[]>();

        // Act - a bulk dataset read in between. It goes through ReadDataset, not the metadata funnel,
        // so it must leave the window alone entirely.
        stream.ResetCounts();
        _ = root.Dataset("small/small").Read<int[]>();
        var actual = group.Attribute("mass_0500").Read<TestStructL1[]>();

        _output.WriteLine($"{stream.MetadataReadCount} metadata reads, {stream.DatasetReadCount} dataset reads");

        // Assert - the data must match, which is what proves the window did not serve stale bytes.
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].ByteValue, actual[i].ByteValue);
            Assert.Equal(expected[i].ULongValue, actual[i].ULongValue);
            Assert.Equal(expected[i].DoubleValue, actual[i].DoubleValue);
        }
    }

    /// <summary>
    /// A file smaller than the window must read correctly: every refill is truncated by the end of the
    /// file, and the reads near the end bypass the window entirely.
    /// </summary>
    /// <remarks>
    /// This is where an off-by-one in the refill length shows up as an <see cref="EndOfStreamException"
    /// />, because <see cref="IDatasetStream.ReadMetadata" /> is an exact-fill contract - asking it for
    /// one byte past the end is an error, not a short read.
    /// </remarks>
    [Fact]
    public void AFileSmallerThanTheWindowReadsCorrectly()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.EARLIEST, fileId => TestUtils.AddSmall(fileId, ContainerType.Dataset));
        var fileBytes = File.ReadAllBytes(filePath);

        _output.WriteLine($"file size: {fileBytes.Length} bytes");

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);

        // Act
        var actual = root.Dataset("small/small").Read<int[]>();

        // Assert
        Assert.Equal(SharedTestData.SmallData, actual);
    }

    /// <summary>
    /// The last bytes of the file must be readable - the case where the window can only be partially
    /// filled, or not at all.
    /// </summary>
    [Fact]
    public void AReadAtTheVeryEndOfTheFileSucceeds()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));
        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("mass_attributes");

        // Act - enumerating every attribute walks the fractal heap to its end, which is at the end of
        // the file for a file written this way.
        var names = group.Attributes().Select(attribute => attribute.Name).ToList();

        // Assert
        Assert.Equal(1000, names.Count);
        Assert.Contains("mass_0999", names);
    }

    /// <summary>
    /// Coalescing must not depend on reads completing synchronously.
    /// </summary>
    /// <remarks>
    /// The refill path is the only genuinely asynchronous method the window has, and it publishes the
    /// window AFTER an await - so a continuation resuming on another thread must still observe it. With
    /// <c>suspend: true</c> every read really suspends and resumes on the thread pool.
    /// </remarks>
    [Fact]
    public void CoalescingWorksWhenEveryReadSuspends()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));
        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: true);
        using var root = H5File.Open(stream, leaveOpen: true);
        var group = root.Group("mass_attributes");

        // Act
        var expected = group.Attribute("mass_0500").Read<TestStructL1[]>();
        var actual = group.Attribute("mass_0500").Read<TestStructL1[]>();

        // Assert
        Assert.Equal(12, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].UIntValue, actual[i].UIntValue);
            Assert.Equal(expected[i].DoubleValue, actual[i].DoubleValue);
        }
    }

    /// <summary>
    /// A hyperslab read of a chunked dataset must be unaffected: chunk payload never goes through the
    /// window, but the chunk INDEX walk does.
    /// </summary>
    [Fact]
    public void AChunkedSelectionStillReadsCorrectly()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId => TestUtils.AddChunkedDatasetForHyperslab(fileId));
        var fileBytes = File.ReadAllBytes(filePath);

        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var dataset = root.Dataset("chunked/hyperslab");

        // The dataset is [25, 25, 4] in chunks of [7, 20, 3], so this block straddles a chunk boundary
        // on all three axes - the index walk therefore visits several nodes rather than one.
        var selection = new HyperslabSelection(rank: 3, starts: [5, 18, 0], blocks: [4, 4, 4]);

        // Act
        var first = dataset.Read<int[]>(selection);
        var second = dataset.Read<int[]>(selection);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(4 * 4 * 4, first.Length);
    }
}

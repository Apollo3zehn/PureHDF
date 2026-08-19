using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading.Async;

/// <summary>
/// Covers the async navigation surface - <c>GetAsync</c>, <c>LinkExistsAsync</c>,
/// <c>ChildrenAsync</c>, <c>AttributeAsync</c>, <c>AttributeExistsAsync</c>, <c>AttributesAsync</c> -
/// which threw <see cref="NotImplementedException" /> until the read path below it was converted.
/// </summary>
/// <remarks>
/// Every test asserts PARITY: the async method must produce what its synchronous counterpart
/// produces. That is the property worth guarding, because the two share one implementation and differ
/// only in whether the boundary blocks - so a divergence means the shared core was broken for one of
/// them, which is exactly the failure a refactor of this shape can introduce.
/// <para>
/// The storage form is varied deliberately rather than incidentally. A group stores its links in a
/// symbol table (V18 and below, reached through the scratch pad or a symbol table message) or densely
/// in a fractal heap plus b-tree v2 (V110), and an object stores attributes compactly in its header or
/// densely in the same fractal-heap machinery. Those are different code paths with different bridges
/// in them, so a test that only exercised one version would leave half the surface unproven.
/// </para>
/// <para>
/// WHAT THESE TESTS DO NOT PROVE: that the async path never blocks. Blocking is only fatal where there
/// is no thread to complete the read on - a single-threaded WASM runtime - and that cannot be
/// reproduced here, because the library awaits with <c>ConfigureAwait(false)</c> and a test host
/// always has a thread pool to run those continuations on. A bridge left behind on one of these paths
/// would therefore still pass. What is verified instead is that these paths tolerate reads which
/// genuinely suspend (see <see cref="AsyncNavigationWorksWhenEveryReadSuspends" />) and that they
/// never reach for the stream cursor.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class AsyncNavigationTests
{
    // Symbol-table storage and dense storage. V18 is the highest version that still writes a symbol
    // table for a small group, V110 writes the fractal heap / b-tree v2 form.
    private static readonly H5F.libver_t[] _versions = [H5F.libver_t.V18, H5F.libver_t.V110];

    [Fact]
    public async Task GetAsyncMatchesGet()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

            // Act
            var expected = root.Group("mass_links/mass_0500");
            var actual = await root.GetAsync("mass_links/mass_0500");

            // Assert
            Assert.Equal(expected.Name, actual.Name);
            Assert.IsType<NativeGroup>(actual);
        }
    }

    [Fact]
    public async Task GetAsyncThrowsForAMissingPath()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

            // Act / Assert - the same failure the synchronous Get reports.
            Assert.Throws<Exception>(() => root.Group("mass_links/mass_1000"));
            await Assert.ThrowsAsync<Exception>(() => root.GetAsync("mass_links/mass_1000"));
        }
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/mass_links", true)]
    [InlineData("/mass_links/mass_0000", true)]
    [InlineData("/mass_links/mass_0500", true)]
    [InlineData("/mass_links/mass_0999", true)]
    [InlineData("/mass_links/mass_1000", false)]
    public async Task LinkExistsAsyncMatchesLinkExists(string path, bool expected)
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

            // Act
            var actual = await root.LinkExistsAsync(path);

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(root.LinkExists(path), actual);
        }
    }

    [Fact]
    public async Task ChildrenAsyncMatchesChildren()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("mass_links");

            // Act
            var expected = group.Children().Select(child => child.Name).ToList();
            var actual = (await group.ChildrenAsync()).Select(child => child.Name).ToList();

            // Assert - order matters: both walk the same index in the same direction.
            Assert.Equal(1000, actual.Count);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// A soft link resolves by walking a path, so following one from the async surface re-enters the
    /// whole navigation stack a second time - the case most likely to have kept a bridge.
    /// </summary>
    [Fact]
    public async Task GetAsyncFollowsSoftLinks()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddLinks);

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

            // Act
            var hard = await root.GetAsync("links/hard_link_1/dataset");
            var soft = await root.GetAsync("links/soft_link_2/dataset");
            var direct = await root.GetAsync("links/dataset");

            // Assert
            Assert.IsType<NativeDataset>(hard);
            Assert.IsType<NativeDataset>(soft);
            Assert.IsType<NativeDataset>(direct);
        }
    }

    /// <summary>
    /// An external link crosses into another <c>NativeFile</c>, which the walk must continue on that
    /// file's own driver rather than the current operation's.
    /// </summary>
    [Fact]
    public async Task GetAsyncFollowsExternalLinks()
    {
        // Arrange
        var externalFilePath = Path.GetTempFileName();
        var externalFileId = H5F.create(externalFilePath, H5F.ACC_TRUNC);
        var externalGroupId1 = H5G.create(externalFileId, "external");
        var externalGroupId2 = H5G.create(externalGroupId1, "group");

        var spaceId = H5S.create_simple(1, [1], [1]);
        var datasetId = H5D.create(externalGroupId2, "external dataset", H5T.NATIVE_UINT, spaceId);

        _ = H5S.close(spaceId);
        _ = H5D.close(datasetId);
        _ = H5G.close(externalGroupId2);
        _ = H5G.close(externalGroupId1);
        _ = H5F.close(externalFileId);

        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddExternalFileLink(fileId, externalFilePath));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

        // Act
        var actual = await root.GetAsync("/links/external_link/external dataset");

        // Assert
        Assert.IsType<NativeDataset>(actual);
    }

    [Fact]
    public async Task GetAsyncReportsADanglingLinkTheSameWay()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddExternalFileLink(fileId, "not-existing.h5"));

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

        // Act
        var actual = await root.GetAsync("/links/external_link") as IH5UnresolvedLink;

        // Assert
        Assert.NotNull(actual);
        Assert.Equal("Could not find file not-existing.h5.", actual!.Reason!.Message);
    }

    [Fact]
    public async Task AttributeAsyncMatchesAttributeWhenStoredDensely()
    {
        // EARLIEST is absent on purpose: 1000 attributes do not fit in a 64 KB object header, so it
        // has no dense attribute storage to test.
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(
                version,
                fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("mass_attributes");

            // Act
            var expected = group.Attribute("mass_0500");
            var actual = await group.AttributeAsync("mass_0500");

            // Assert
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Type.Class, actual.Type.Class);
        }
    }

    [Fact]
    public async Task AttributeAsyncMatchesAttributeWhenStoredCompactly()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var expected = SharedTestData.SmallData;

            var filePath = TestUtils.PrepareTestFile(
                version,
                fileId => TestUtils.Add(
                    ContainerType.Attribute, fileId, "compact", "attribute",
                    H5T.NATIVE_INT32, expected.AsSpan()));

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("compact");

            // Act
            var attribute = await group.AttributeAsync("attribute");
            var actual = attribute.Read<int[]>();

            // Assert
            Assert.Equal("attribute", attribute.Name);
            Assert.True(actual.SequenceEqual(expected));
        }
    }

    [Fact]
    public async Task AttributeAsyncThrowsForAMissingAttribute()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(
                version,
                fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("mass_attributes");

            // Act / Assert
            await Assert.ThrowsAsync<Exception>(() => group.AttributeAsync("mass_1000"));
        }
    }

    [Theory]
    [InlineData("mass_0000", true)]
    [InlineData("mass_0500", true)]
    [InlineData("mass_0999", true)]
    [InlineData("mass_1000", false)]
    public async Task AttributeExistsAsyncMatchesAttributeExists(string name, bool expected)
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(
                version,
                fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("mass_attributes");

            // Act
            var actual = await group.AttributeExistsAsync(name);

            // Assert
            Assert.Equal(expected, actual);
            Assert.Equal(group.AttributeExists(name), actual);
        }
    }

    [Fact]
    public async Task AttributesAsyncMatchesAttributes()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(
                version,
                fileId => TestUtils.AddMass(fileId, ContainerType.Attribute));

            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var group = root.Group("mass_attributes");

            // Act
            var expected = group.Attributes().Select(attribute => attribute.Name).ToList();
            var actual = (await group.AttributesAsync()).Select(attribute => attribute.Name).ToList();

            // Assert
            Assert.Equal(1000, actual.Count);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// The same navigation, against a stream whose every read completes asynchronously rather than
    /// synchronously.
    /// </summary>
    /// <remarks>
    /// Two things are checked at once. The stream throws from every cursor-based
    /// <see cref="Stream" /> member, so any read still going through the cursor fails loudly. And
    /// because no read completes inline, a path that resumed on the wrong state - a b-tree walk whose
    /// cursor assumption only held while reads were instantaneous, say - shows up here and not in the
    /// tests above.
    /// </remarks>
    [Fact]
    public async Task AsyncNavigationWorksWhenEveryReadSuspends()
    {
        foreach (var version in _versions)
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

            try
            {
                using var stream = new ConcurrentStream(File.ReadAllBytes(filePath), suspend: true);
                using var root = H5File.Open(stream);

                var group = (NativeGroup)await root.GetAsync("mass_links");

                // Act
                var exists = await group.LinkExistsAsync("mass_0500");
                var child = await group.GetAsync("mass_0500");
                var children = (await group.ChildrenAsync()).Select(current => current.Name).ToList();

                // Assert
                Assert.True(exists);
                Assert.Equal("mass_0500", child.Name);
                Assert.Equal(1000, children.Count);
                Assert.Contains("mass_0500", children);
            }

            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task AsyncEnumerationsObserveCancellation()
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(H5F.libver_t.V110, fileId =>
        {
            TestUtils.AddMassLinks(fileId);
            TestUtils.AddMass(fileId, ContainerType.Attribute);
        });

        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var token = cancellationTokenSource.Token;

        // Act / Assert - cancellation is observed per link and per attribute, so an already-cancelled
        // token stops both enumerations before they yield anything.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => root.Group("mass_links").ChildrenAsync(token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => root.Group("mass_attributes").AttributesAsync(token));
    }
}

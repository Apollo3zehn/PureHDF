using HDF.PInvoke;
using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading;

/// <summary>
/// Measures what a REPEATED navigation call costs in structural reads.
/// </summary>
/// <remarks>
/// The unit is <c>IDatasetStream.ReadMetadata</c> calls, not time. A read count is deterministic and
/// machine-independent; a timing on a laptop with an active frequency governor is not, and during
/// this work a benchmark difference that looked real turned out to be governor drift. So the guard
/// that protects this behavior counts reads.
/// <para>
/// Every case warms up first and then measures ONE more identical call, because the interesting
/// quantity is the marginal cost of navigating again - the first call has to decode the object
/// header regardless, and no caching can remove that. What a cache removes is the second call's
/// re-decode of the link or attribute storage it already walked.
/// </para>
/// <para>
/// The numbers below are descriptive, not normative: they document what the current implementation
/// does so that a change which alters them has to say so out loud. The direction that matters is
/// down for the by-name cases and unchanged for the enumeration cases.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class NavigationCostTests
{
    // Deliberately not 450: TestUtils.AddMass gives that one index a UTF-8 name.
    private const string TARGET = "mass_0500";

    private readonly ITestOutputHelper _output;

    public NavigationCostTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RepeatedNavigationHasAKnownCost()
    {
        // Act
        var actual = new[]
        {
            // Links, by name, against a group the caller holds. EARLIEST stores them in a symbol
            // table (local heap + b-tree v1, reached through the group's ObjectHeaderScratchPad);
            // V110 stores 1000 links densely (fractal heap + b-tree v2, reached through
            // LinkInfoMessage).
            Measure("link by name, symbol table", H5F.libver_t.EARLIEST, TestUtils.AddMassLinks,
                root =>
                {
                    var group = root.Group("mass_links");
                    return () => group.Group(TARGET);
                }),

            Measure("link by name, dense", H5F.libver_t.V110, TestUtils.AddMassLinks,
                root =>
                {
                    var group = root.Group("mass_links");
                    return () => group.Group(TARGET);
                }),

            // The same lookup reached by path from the file rather than from a retained group. This
            // re-dereferences the intermediate group every time, so no cache on the group can help
            // it - it is here to show that, and to catch a change that made it worse.
            Measure("link by path, symbol table", H5F.libver_t.EARLIEST, TestUtils.AddMassLinks,
                root => () => root.Group($"mass_links/{TARGET}")),

            Measure("link by path, dense", H5F.libver_t.V110, TestUtils.AddMassLinks,
                root => () => root.Group($"mass_links/{TARGET}")),

            // Enumeration walks the whole link storage once per call and decodes 1000 children, so it
            // dwarfs any per-call re-decode - which is why it was included as the control that ought
            // not to move. The dense case then halved anyway: an enumeration resolves a fractal heap
            // ID per child and every one of those walked back through the same handful of heap blocks,
            // so caching the blocks helps WITHIN a single call and not only between calls. Worth
            // stating plainly rather than quietly updating the number: the control assumption was
            // wrong, not the measurement.
            Measure("children, symbol table", H5F.libver_t.EARLIEST, TestUtils.AddMassLinks,
                root =>
                {
                    var group = root.Group("mass_links");
                    return () => group.Children().ToList();
                }),

            Measure("children, dense", H5F.libver_t.V110, TestUtils.AddMassLinks,
                root =>
                {
                    var group = root.Group("mass_links");
                    return () => group.Children().ToList();
                }),

            // Attributes, by name, against a group the caller holds. EARLIEST is absent because it
            // has no dense attribute storage at all - 1000 attributes do not fit in a 64 KB object
            // header - so V18 and V110 are the two versions that reach AttributeInfoMessage.
            Measure("attribute by name, dense", H5F.libver_t.V18, AddMassAttributes,
                root =>
                {
                    var group = root.Group("mass_attributes");
                    return () => group.Attribute(TARGET);
                }),

            Measure("attribute by name, dense, V110", H5F.libver_t.V110, AddMassAttributes,
                root =>
                {
                    var group = root.Group("mass_attributes");
                    return () => group.Attribute(TARGET);
                }),

            // The enumeration counterpart for the attribute path. Note the magnitude: enumerating 1000
            // dense attributes still costs ~363k structural reads, ~363 per attribute, by far the most
            // expensive navigation in the library. That is a pre-existing cost, only partly addressed
            // here, and it is recorded mainly so the size of it is on the record.
            Measure("attributes, dense", H5F.libver_t.V110, AddMassAttributes,
                root =>
                {
                    var group = root.Group("mass_attributes");
                    return () => group.Attributes().ToList();
                })
        };

        // Reported unconditionally, so that the current cost of every case is visible from a passing
        // run and not only from a failing one.
        foreach (var measurement in actual)
        {
            _output.WriteLine(measurement);
        }

        // Assert
        string[] expected =
        [
            "link by name, symbol table: 32",
            "link by name, dense: 59",
            "link by path, symbol table: 56",
            "link by path, dense: 95",
            "children, symbol table: 8557",
            "children, dense: 34156",
            "attribute by name, dense: 359",
            "attribute by name, dense, V110: 359",
            "attributes, dense: 363269"
        ];

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The same measurement for a repeated <c>Read</c> of a chunked dataset, across all four chunk
    /// index forms.
    /// </summary>
    /// <remarks>
    /// Not navigation, but the same shape of cost and the same instrument. It lives here rather than in
    /// a benchmark because the benchmark project writes its datasets with PureHDF's own writer, which
    /// produces a CONTIGUOUS layout - so no benchmark in the repo reads a chunked dataset at all, and
    /// this cost was invisible.
    /// <para>
    /// What makes it repeat: <c>NativeDataset.Read</c> builds a fresh <c>H5D_Base</c> per call, so the
    /// chunk index it decodes into that object's field was thrown away after every read.
    /// </para>
    /// </remarks>
    [Fact]
    public void RepeatedChunkedReadHasAKnownCost()
    {
        // Act
        var actual = new[]
        {
            MeasureRead("chunk index, b-tree v1", H5F.libver_t.V18,
                fileId => TestUtils.AddChunkedDataset_Legacy(fileId, withShuffle: false),
                "chunked/chunked"),

            MeasureRead("chunk index, b-tree v2", H5F.libver_t.LATEST,
                fileId => TestUtils.AddChunkedDataset_BTree2(fileId, withShuffle: false),
                "chunked/chunked_btree2"),

            MeasureRead("chunk index, fixed array", H5F.libver_t.LATEST,
                fileId => TestUtils.AddChunkedDataset_Fixed_Array(fileId, withShuffle: false),
                "chunked/chunked_fixed_array"),

            MeasureRead("chunk index, extensible array", H5F.libver_t.LATEST,
                fileId => TestUtils.AddChunkedDataset_Extensible_Array_Elements(fileId, withShuffle: false),
                "chunked/chunked_extensible_array_elements")
        };

        foreach (var measurement in actual)
        {
            _output.WriteLine(measurement);
        }

        // Assert
        //
        // Both b-tree forms reach zero: a repeated read of a chunked dataset now decodes no structural
        // bytes at all. The two array forms only got partway, because only their HEADER is cached -
        // their index and data blocks are still decoded per chunk lookup, which is the same kind of
        // gap the b-tree leaf node was.
        string[] expected =
        [
            "chunk index, b-tree v1: 0",
            "chunk index, b-tree v2: 0",
            "chunk index, fixed array: 11",
            "chunk index, extensible array: 62"
        ];

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Builds a file, resolves whatever <paramref name="arrange" /> retains, then reports how many
    /// structural reads ONE further identical navigation call costs.
    /// </summary>
    private static string Measure(
        string label,
        H5F.libver_t version,
        Action<long> build,
        Func<NativeFile, Action> arrange)
    {
        var filePath = TestUtils.PrepareTestFile(version, build);

        try
        {
            using var stream = new PositionlessDatasetStream(File.ReadAllBytes(filePath), suspend: false);
            using var root = H5File.Open(stream);

            var navigate = arrange(root);

            // Warm-up. Anything a first call must do no matter what - decoding the object header,
            // populating the file-level caches - happens here and is deliberately not measured.
            navigate();

            stream.ResetCounts();
            navigate();

            // Navigation must not touch the bulk-payload path at all. If it does, the split between
            // the two IDatasetStream methods has stopped meaning what it says and the metadata count
            // has quietly become an undercount.
            Assert.Equal(0, stream.DatasetReadCount);

            return $"{label}: {stream.MetadataReadCount}";
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// Builds a file, reads <paramref name="datasetPath" /> once, then reports how many structural
    /// reads ONE further identical read costs. Dataset-payload reads are excluded by construction -
    /// they are the same on both reads and are counted separately by the stream.
    /// </summary>
    private static string MeasureRead(
        string label,
        H5F.libver_t version,
        Action<long> build,
        string datasetPath)
    {
        var filePath = TestUtils.PrepareTestFile(version, build);

        try
        {
            using var stream = new PositionlessDatasetStream(File.ReadAllBytes(filePath), suspend: false);
            using var root = H5File.Open(stream);

            var dataset = root.Dataset(datasetPath);

            // Warm-up: resolves the dataset and pays every one-time decode.
            dataset.Read<int[]>();

            stream.ResetCounts();
            dataset.Read<int[]>();

            return $"{label}: {stream.MetadataReadCount}";
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static void AddMassAttributes(long fileId)
    {
        TestUtils.AddMass(fileId, ContainerType.Attribute);
    }
}

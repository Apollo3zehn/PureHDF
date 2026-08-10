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

            // Enumeration walks the whole link storage once per call and decodes 1000 children, so
            // it dwarfs any per-call re-decode. Included as the control: these two must NOT move.
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

            // The enumeration control for the attribute path. Note the magnitude: enumerating 1000
            // dense attributes costs ~450k structural reads, ~450 per attribute, which is a
            // pre-existing cost unrelated to this work and by far the most expensive navigation in
            // the library. Recorded here because it is worth knowing about, not because it changes.
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
            "link by name, symbol table: 120",
            "link by name, dense: 261",
            "link by path, symbol table: 153",
            "link by path, dense: 297",
            "children, symbol table: 8582",
            "children, dense: 76206",
            "attribute by name, dense: 667",
            "attribute by name, dense, V110: 667",
            "attributes, dense: 450860"
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

    private static void AddMassAttributes(long fileId)
    {
        TestUtils.AddMass(fileId, ContainerType.Attribute);
    }
}

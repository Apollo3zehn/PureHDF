using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Writing;

/// <summary>
/// Baseline measurements for the writer's metadata layout, so the effect of changing it is a number
/// rather than an impression.
/// </summary>
/// <remarks>
/// Shaped after a real 620 MB reporting file: many small groups, each carrying the same
/// assembly-qualified type-hint string as an attribute. Kept small enough to run in the suite.
/// </remarks>
public class MetadataLayoutTests(ITestOutputHelper output)
{
    private const int GroupCount = 2_000;

    // 146 characters, the length of the assembly-qualified type name the reporting library writes.
    private const int HintLength = 146;

    private static string TypeHint(int i) =>
        $"Ranovus.Reporting.Model.Measurement{i:D6}, Ranovus.Reporting, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            .PadRight(HintLength, '.')[..HintLength];

    private static readonly string _sharedHint = TypeHint(0);

    /// <summary>
    /// Compact layout is off for every file here, and that is load-bearing rather than incidental.
    /// </summary>
    /// <remarks>
    /// A compact dataset's payload is embedded in its object header - see the compact branch in
    /// <c>DataLayoutMessage4.Writing</c>, which makes no allocation of its own - so for datasets under
    /// 64 kB, payload IS metadata and there is nothing for a placement to separate. Left on, these
    /// files would be almost entirely "metadata" and the measurement would say nothing. The real
    /// reporting files are chunked and filtered, so they take the contiguous/chunked path modelled here.
    /// </remarks>
    private static H5WriteOptions Options(H5MetadataPlacement placement = H5MetadataPlacement.Interleaved) => new()
    {
        PreferCompactDatasetLayout = false,
        MetadataPlacement = placement,

        // Scaled to these files. The waste is bounded by the block size, so on a 16 MB file an 8 MB
        // default block would cost 50% while on the 620 MB file it costs 1.3%.
        MetadataBlockSize = 1024 * 1024,

        // Deliberately generous. h5stat reports 522,088 bytes of "File metadata" for these files,
        // but the ALLOCATED footprint is larger than that figure: global heap collections are
        // allocated at a 4 kB minimum each and mostly left partly empty, which h5stat counts as
        // unaccounted space rather than metadata. So 1 MB against a 522 kB report does not cover these
        // files: the shortfall spills into a second region, which is the graceful path but costs the
        // locality this is measuring. Size a reservation against measured file growth, not against the
        // metadata line.
        MetadataReservation = 3 * 1024 * 1024
    };

    private static byte[] Write(Func<int, string> hint, int payloadPerGroup, H5WriteOptions? options = null)
    {
        var root = new H5File();

        for (int i = 0; i < GroupCount; i++)
        {
            var group = new H5Group
            {
                // A little raw data per group, so metadata and data genuinely interleave.
                ["values"] = new H5Dataset(Enumerable.Range(i, payloadPerGroup).ToArray())
            };

            group.Attributes = new Dictionary<string, object>
            {
                ["Meta::TypeHint"] = hint(i)
            };

            root[$"unit{i:D5}"] = group;
        }

        var filePath = Path.GetTempFileName();

        try
        {
            root.Write(filePath, options);

            return File.ReadAllBytes(filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// How many 1 MiB slots a reader must fetch to see every byte of file structure.
    /// </summary>
    /// <remarks>
    /// The proxy for structural locality: object header signatures ("OHDR") and global heap collections
    /// ("GCOL") are located, then mapped onto fixed-size slots. Touching every slot means a
    /// range-request reader downloads the whole file to read the structure.
    /// </remarks>
    private static (int Touched, int Total) SlotsTouched(byte[] bytes, int slotSize)
    {
        var slots = new HashSet<int>();

        void Mark(byte[] signature)
        {
            for (int i = 0; i + signature.Length <= bytes.Length; i++)
            {
                var match = true;

                for (int j = 0; j < signature.Length; j++)
                {
                    if (bytes[i + j] != signature[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    slots.Add(i / slotSize);
            }
        }

        Mark("OHDR"u8.ToArray());
        Mark("GCOL"u8.ToArray());

        return (slots.Count, (bytes.Length + slotSize - 1) / slotSize);
    }

    /// <summary>
    /// The point of the whole exercise: structure must stop touching every range of the file.
    /// </summary>
    /// <remarks>
    /// Asserts a ratio rather than absolute slot counts, so it keeps its meaning if the writer's
    /// encoding changes size. Each placement is also read back in full, because a layout that loses
    /// locality is a disappointment while a layout that loses data is a catastrophe - and the two
    /// failures look identical from a slot count.
    /// </remarks>
    [Theory]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void PlacementClustersStructure(H5MetadataPlacement placement)
    {
        const int slotSize = 1024 * 1024;

        var interleaved = Write(_ => _sharedHint, payloadPerGroup: 2_000, Options());
        var clustered = Write(_ => _sharedHint, payloadPerGroup: 2_000, Options(placement));

        var (before, beforeTotal) = SlotsTouched(interleaved, slotSize);
        var (after, afterTotal) = SlotsTouched(clustered, slotSize);

        output.WriteLine($"interleaved: {interleaved.Length,12:N0} bytes, structure in {before}/{beforeTotal} slots");
        output.WriteLine($"{placement,-12}: {clustered.Length,12:N0} bytes, structure in {after}/{afterTotal} slots");
        output.WriteLine($"file size cost: {(clustered.Length - interleaved.Length) / (double)interleaved.Length,8:P2}");

        // Interleaved is expected to touch everything - that is the defect being fixed. If it ever
        // stops doing so, this comparison has quietly become meaningless.
        Assert.Equal(beforeTotal, before);

        // Structure should fit in well under half the file's ranges.
        Assert.True(
            after * 2 < afterTotal,
            $"{placement} touched {after} of {afterTotal} slots, expected fewer than half.");

        // Every group must still read back correctly, attribute included.
        using var root = H5File.Open(new MemoryStream(clustered), leaveOpen: true);

        for (int i = 0; i < GroupCount; i += 250)
        {
            var group = root.Group($"unit{i:D5}");

            Assert.Equal(_sharedHint, group.Attribute("Meta::TypeHint").Read<string>());
            Assert.Equal(Enumerable.Range(i, 2_000).ToArray(), group.Dataset("values").Read<int[]>());
        }
    }

    /// <summary>
    /// The HDF5 C library must accept every placement, not just PureHDF's own reader.
    /// </summary>
    /// <remarks>
    /// The layouts here are unusual in two ways the format permits but nothing else in this suite
    /// exercises: structure sits before payload rather than beside it, and a reserved region can leave a
    /// hole that nothing points into while the declared end-of-file address still covers it. Reading the
    /// file back with PureHDF cannot catch a disagreement between the two implementations about either,
    /// so this shells out to h5dump - the same tool the writer's dump fixtures use.
    /// </remarks>
    [SkippableTheory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void TheCLibraryReadsEveryPlacement(H5MetadataPlacement placement)
    {
        // Arrange - small, because h5dump prints every value it finds.
        var root = new H5File
        {
            ["group"] = new H5Group
            {
                ["values"] = new H5Dataset(Enumerable.Range(0, 64).ToArray()),
                ["Meta::TypeHint"] = _sharedHint
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            root.Write(filePath, Options(placement));

            // Act
            var dump = TestUtils.DumpH5File(filePath);

            Skip.If(dump is null, "h5dump is not available.");

            // Assert - it parsed the file and found the contents, rather than reporting a bad superblock
            // or a truncated file.
            Assert.DoesNotContain("error", dump, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("values", dump);
            Assert.Contains("Meta::TypeHint", dump);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// Front-loading is the default, and every placement is reachable through the options.
    /// </summary>
    [Fact]
    public void TheDefaultIsFrontLoadedAndInterleavedIsStillAvailable()
    {
        Assert.Equal(H5MetadataPlacement.FrontLoaded, new H5WriteOptions().MetadataPlacement);
        Assert.Equal(0, new H5WriteOptions().MetadataReservation);

        var interleaved = Write(_ => _sharedHint, payloadPerGroup: 500, new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.Interleaved
        });

        var (touched, total) = SlotsTouched(interleaved, 256 * 1024);

        // Opting out really opts out: structure is spread across the file again.
        Assert.Equal(total, touched);
    }

    /// <summary>
    /// With no reservation given, the writer measures the file rather than estimating it - so the
    /// reservation should be tight enough to cost about what aggregation costs, not multiples of it.
    /// </summary>
    /// <remarks>
    /// This is the assertion that pins the reservation to a measurement rather than to a model of the
    /// object graph. Two things put such a model out by multiples: a chunk index's size scales with chunk
    /// count rather than being constant, and global heap collections are allocated at a 4 kB minimum each
    /// and left partly empty. Encoding against a discarding stream sees both, because it is the same
    /// encoder and the same allocator.
    /// </remarks>
    [Fact]
    public void AutoSizedFrontLoadingCostsAboutWhatAggregationCosts()
    {
        const int slotSize = 1024 * 1024;

        var interleaved = Write(_ => _sharedHint, payloadPerGroup: 2_000, Options());

        var autoSized = Write(_ => _sharedHint, payloadPerGroup: 2_000, new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.FrontLoaded

            // MetadataReservation deliberately unset: this is the path under test.
        });

        var (before, beforeTotal) = SlotsTouched(interleaved, slotSize);
        var (after, afterTotal) = SlotsTouched(autoSized, slotSize);
        var overhead = (autoSized.Length - interleaved.Length) / (double)interleaved.Length;

        output.WriteLine($"interleaved      : {interleaved.Length,12:N0} bytes, structure in {before}/{beforeTotal} slots");
        output.WriteLine($"front-loaded auto: {autoSized.Length,12:N0} bytes, structure in {after}/{afterTotal} slots");
        output.WriteLine($"file size cost   : {overhead,8:P2}");

        // Locality: the whole point.
        Assert.True(after * 2 < afterTotal, $"structure touched {after} of {afterTotal} slots.");

        // Size: a measured reservation is exact, so the only waste is the fixed slack the writer adds
        // to keep the final allocation from spilling - a constant, not a fraction of the file. That
        // makes this cheaper than aggregation, which pays for a whole unused block.
        Assert.True(overhead < 0.005, $"auto-sized front loading cost {overhead:P2}, expected under 0.5%.");

        // And it must still be a correct file.
        using var root = H5File.Open(new MemoryStream(autoSized), leaveOpen: true);

        for (int i = 0; i < GroupCount; i += 250)
        {
            var group = root.Group($"unit{i:D5}");

            Assert.Equal(_sharedHint, group.Attribute("Meta::TypeHint").Read<string>());
            Assert.Equal(Enumerable.Range(i, 2_000).ToArray(), group.Dataset("values").Read<int[]>());
        }
    }



    [Fact]
    public void MeasureBaseline()
    {
        const int slotSize = 1024 * 1024;

        var shared = Write(_ => _sharedHint, payloadPerGroup: 2_000);
        var distinct = Write(TypeHint, payloadPerGroup: 2_000);

        var (sharedTouched, sharedTotal) = SlotsTouched(shared, slotSize);
        var (distinctTouched, distinctTotal) = SlotsTouched(distinct, slotSize);

        output.WriteLine($"{GroupCount} groups, identical type hint : {shared.Length,12:N0} bytes, structure in {sharedTouched}/{sharedTotal} slots of 1 MiB");
        output.WriteLine($"{GroupCount} groups, distinct type hints : {distinct.Length,12:N0} bytes, structure in {distinctTouched}/{distinctTotal} slots of 1 MiB");
        output.WriteLine($"difference attributable to repeated string payload: {distinct.Length - shared.Length,12:N0} bytes");
        output.WriteLine($"payload if every copy were stored: {(long)GroupCount * 146,12:N0} bytes");

        // No assertion on the numbers - this test exists to report them. It asserts only that the
        // files are readable, so it cannot silently measure garbage.
        using var root = H5File.Open(new MemoryStream(shared), leaveOpen: true);
        Assert.Equal(_sharedHint, root.Group("unit00000").Attribute("Meta::TypeHint").Read<string>());
    }
}

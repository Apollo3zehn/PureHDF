using PureHDF.Filters;
using PureHDF.VOL.Native;
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

    // 146 characters, the length of an assembly-qualified type name of the kind a serializer writes.
    private const int HintLength = 146;

    private static string TypeHint(int i) =>
        $"Example.Model.Measurement{i:D6}, Example.Model, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            .PadRight(HintLength, '.')[..HintLength];

    private static readonly string _sharedHint = TypeHint(0);

    /// <summary>
    /// Compact layout is off for every file here, and that is load-bearing rather than incidental.
    /// </summary>
    /// <remarks>
    /// A compact dataset's payload is embedded in its object header - see the compact branch in
    /// <c>DataLayoutMessage4.Writing</c>, which makes no allocation of its own - so for datasets under
    /// 64 kB, payload IS metadata and there is nothing for a placement to separate. Left on, these
    /// files would be almost entirely "metadata" and the measurement would say nothing. Files large
    /// enough for placement to matter are typically chunked and filtered, so they take the
    /// contiguous/chunked path modelled here.
    /// </remarks>
    private static H5WriteOptions Options(H5MetadataPlacement placement = H5MetadataPlacement.Interleaved) => new()
    {
        PreferCompactDatasetLayout = false,
        MetadataPlacement = placement,

        // Lowered from the 8 MB default only to keep the ceiling in view on files this size. Blocks
        // double up to the ceiling rather than starting at it, so the default would not inflate these
        // files either - it would never be reached.
        MetadataBlockSize = 1024 * 1024,

        // An explicit reservation, so these files exercise the path that skips the sizing pass. 1 MB
        // against a measured need of 878,158 bytes, which covers it with room to spare - and the room
        // is the point of the setting rather than a flaw in it: an explicit reservation abandons its
        // unused tail, which is the price of not measuring. Size one against measured file growth
        // rather than against h5stat's "File metadata" line, which reports 522,088 bytes here: global
        // heap collections are allocated at a 4 kB minimum each and mostly left partly empty, and
        // h5stat counts that as unaccounted space rather than as metadata.
        MetadataReservation = 1024 * 1024
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
    /// The proxy for structural locality: object headers ("OHDR") and global heap collections ("GCOL")
    /// are located by their signatures, then mapped onto fixed-size slots. An object header is mapped
    /// with its whole first chunk, since a group with many links has a header large enough to cross a
    /// slot boundary. Touching every slot means a range-request reader downloads the whole file to read
    /// the structure.
    /// </remarks>
    private static (int Touched, int Total) SlotsTouched(byte[] bytes, int slotSize)
    {
        var slots = new HashSet<int>();

        void Mark(byte[] signature, Func<int, int>? length = default)
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
                {
                    var end = i + (length?.Invoke(i) ?? signature.Length);

                    for (var slot = i / slotSize; slot <= (end - 1) / slotSize; slot++)
                        slots.Add(slot);
                }
            }
        }

        Mark("OHDR"u8.ToArray(), ObjectHeaderLength);
        Mark("GCOL"u8.ToArray());

        return (slots.Count, (bytes.Length + slotSize - 1) / slotSize);

        // A version 2 object header's prefix, its first chunk and the checksum after it.
        int ObjectHeaderLength(int start)
        {
            var flags = bytes[start + 5];
            var position = start + 6;

            if ((flags & 0x20) != 0)
                position += 16;

            if ((flags & 0x10) != 0)
                position += 4;

            var sizeLength = 1 << (flags & 0x03);
            var chunkSize = 0L;

            for (var k = 0; k < sizeLength; k++)
                chunkSize |= (long)bytes[position + k] << (8 * k);

            return (int)(position + sizeLength + chunkSize + sizeof(uint) - start);
        }
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

        // Interleaved is expected to touch everything - that is what the clustered placements exist to
        // avoid. If it ever stops doing so, this comparison has quietly become meaningless.
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
            var result = TestUtils.RunH5Dump(filePath);

            Skip.If(result is null, "h5dump is not available.");

            // Assert - it parsed the file and found the contents, rather than reporting a bad superblock
            // or a truncated file. Asked of the exit code and the raw error stream rather than of
            // DumpH5File's output, which strips the error stack and so can never contain a complaint.
            Assert.False(result!.Failed, $"h5dump rejected the file:{Environment.NewLine}{result.Diagnostics}");
            Assert.Contains("values", result.Stdout);
            Assert.Contains("Meta::TypeHint", result.Stdout);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// Interleaving is the default, so this feature costs nothing until it is asked for.
    /// </summary>
    /// <remarks>
    /// The default decides the bytes of every file the writer produces, so it is asserted rather than
    /// left to the property initializer. Defaulting to a placement would change those bytes for callers
    /// who never asked for it, and the only ones who benefit are those reading over a high-latency link
    /// - who know that about themselves and can say so.
    /// </remarks>
    [Fact]
    public void InterleavedIsTheDefault()
    {
        Assert.Equal(H5MetadataPlacement.Interleaved, new H5WriteOptions().MetadataPlacement);
        Assert.Equal(0, new H5WriteOptions().MetadataReservation);

        var byDefault = Write(_ => _sharedHint, payloadPerGroup: 500, new H5WriteOptions
        {
            PreferCompactDatasetLayout = false
        });

        var (touched, total) = SlotsTouched(byDefault, 256 * 1024);

        // The default is the encode-order layout: structure spread across every range of the file.
        Assert.Equal(total, touched);
    }

    /// <summary>
    /// With no reservation given, the writer measures the file rather than estimating it - and a
    /// measurement is exact, so front loading costs nothing at all rather than a little.
    /// </summary>
    /// <remarks>
    /// This is the assertion that pins the reservation to a measurement rather than to a model of the
    /// object graph. Two things put such a model out by multiples: a chunk index's size scales with chunk
    /// count rather than being constant, and global heap collections are allocated at a 4 kB minimum each
    /// and left partly empty. Encoding against a discarding stream sees both, because it is the same
    /// encoder and the same allocator.
    /// </remarks>
    [Fact]
    public void AutoSizedFrontLoadingCostsNothing()
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

        // Size: a measured reservation is the exact number of bytes that will be served from it, so this
        // costs nothing at all rather than a little. Asserted as equality, because "close enough" is what
        // hid twelve kilobytes of slack that no test objected to.
        Assert.Equal(interleaved.Length, autoSized.Length);
        Assert.Equal(0, overhead);

        // One region, nothing abandoned: the measurement was neither short (which spills into a second
        // region and loses the locality) nor long (which pays for a tail nothing uses). This is the
        // assertion that makes the two failure modes visible - MetadataRegionsOpened exists to report
        // exactly this and was checked nowhere.
        var probe = new H5File();

        for (int i = 0; i < GroupCount; i++)
        {
            var group = new H5Group
            {
                ["values"] = new H5Dataset(Enumerable.Range(i, 2_000).ToArray())
            };

            group.Attributes = new Dictionary<string, object> { ["Meta::TypeHint"] = _sharedHint };
            probe[$"unit{i:D5}"] = group;
        }

        var probePath = Path.GetTempFileName();

        try
        {
            var writer = probe.BeginWrite(probePath, new H5WriteOptions
            {
                PreferCompactDatasetLayout = false,
                MetadataPlacement = H5MetadataPlacement.FrontLoaded
            });

            writer.Dispose();

            Assert.Equal(1, writer.Context.FreeSpaceManager.MetadataRegionsOpened);
            Assert.Equal(0, writer.Context.FreeSpaceManager.MetadataAbandoned);
        }

        finally
        {
            if (File.Exists(probePath))
                File.Delete(probePath);
        }

        // And it must still be a correct file.
        using var root = H5File.Open(new MemoryStream(autoSized), leaveOpen: true);

        for (int i = 0; i < GroupCount; i += 250)
        {
            var group = root.Group($"unit{i:D5}");

            Assert.Equal(_sharedHint, group.Attribute("Meta::TypeHint").Read<string>());
            Assert.Equal(Enumerable.Range(i, 2_000).ToArray(), group.Dataset("values").Read<int[]>());
        }
    }


    /// <summary>
    /// Every shape the writer can produce, so the sizing pass can be trusted across all of them.
    /// </summary>
    public static TheoryData<string, Func<H5File>> Shapes => new()
    {
        { "contiguous int", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 4096).ToArray()) } },
        { "compact int", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 64).ToArray()) } },
        { "unfiltered chunked int", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 1 << 16).ToArray(), chunks: [4096]) } },
        { "filtered chunked int", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 1 << 16).ToArray(), chunks: [4096], datasetCreation: new(Filters: [DeflateFilter.Id])) } },
        { "filtered single chunk", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 4096).ToArray(), chunks: [4096], datasetCreation: new(Filters: [DeflateFilter.Id])) } },
        { "variable-length strings", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 500).Select(i => new string('x', 120) + i).ToArray()) } },
        { "nullable ints", () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 500).Select(i => (int?)i).ToArray()) } },
        { "string attribute", () => new H5File { ["g"] = new H5Group { Attributes = new Dictionary<string, object> { ["hint"] = _sharedHint } } } },
        { "object reference", () => Referencing() },
        { "many groups", () => ManyGroups() }
    };

    private static H5File Referencing()
    {
        var target = new H5Group { ["values"] = new H5Dataset(Enumerable.Range(0, 4096).ToArray()) };

        return new H5File
        {
            ["target"] = target,
            ["pointer"] = new H5Dataset(new[] { new H5ObjectReference(target) })
        };
    }

    private static H5File ManyGroups()
    {
        var root = new H5File();

        for (int i = 0; i < 200; i++)
        {
            root[$"unit{i:D4}"] = new H5Group
            {
                ["values"] = new H5Dataset(Enumerable.Range(i, 2_000).ToArray()),
                Attributes = new Dictionary<string, object> { ["hint"] = _sharedHint }
            };
        }

        return root;
    }

    private static long MetadataAllocatedByAWrite(H5File file, H5WriteOptions options)
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var writer = file.BeginWrite(filePath, options);
            writer.Dispose();

            return writer.Context.FreeSpaceManager.MetadataAllocated;
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// The sizing pass must report the same total a real write allocates - exactly, for every shape.
    /// </summary>
    /// <remarks>
    /// The load-bearing test for front loading, and the one that lets the pass take shortcuts. It takes
    /// two: it does not compress, and for fixed-size elements it does not touch the data at all. Both
    /// rest on the same claim - that no metadata size depends on a value - and this asserts that claim
    /// per shape rather than arguing it. An inequality would not do: a pass that over-counts wastes the
    /// reservation's tail and one that under-counts spills, so the number has to be the number.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheSizingPassMeasuresExactlyWhatAWriteAllocates(string label, Func<H5File> makeFile)
    {
        var options = new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.Interleaved
        };

        var measured = H5NativeWriter.MeasureMetadataSize(makeFile(), options);
        var allocated = MetadataAllocatedByAWrite(makeFile(), options);

        output.WriteLine($"{label,-24} measured {measured,10:N0}   allocated {allocated,10:N0}");

        Assert.Equal(allocated, measured);
    }

    /// <summary>
    /// Nothing a deferred write does outruns the sizing pass.
    /// </summary>
    /// <remarks>
    /// The pass runs before the caller has written anything through the writer, so the question is which
    /// metadata it can still account for. All of it, as it turns out. A chunk index's size follows from
    /// the chunk count, and that follows from the dimensions, which are fixed when the dataset is
    /// declared - <c>WriteChunkInfos</c> is sized to <c>TotalChunkCount</c> in
    /// <c>H5D_Chunk4.Initialize</c>, and writing data later fills entries in an array whose length was
    /// settled then. And a variable-length value's global heap space is allocated as payload rather than
    /// as structure, so however much of it a caller writes later, the metadata total does not move.
    /// <para>
    /// That is what makes <see cref="H5WriteOptions.MetadataReservation" /> unnecessary for a deferred
    /// write rather than advisable for some of them. Asserted rather than reasoned, because it is the
    /// entire guidance given for deferred writes - and the guidance said the opposite twice before this
    /// test existed.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingDeferredOutrunsTheSizingPass()
    {
        var options = new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.Interleaved
        };

        long Shortfall<T>(string label, Func<H5Dataset<T>> makeDataset, T data)
        {
            var measured = H5NativeWriter.MeasureMetadataSize(
                new H5File { ["d"] = makeDataset() }, options);

            var dataset = makeDataset();
            var filePath = Path.GetTempFileName();

            try
            {
                var writer = new H5File { ["d"] = dataset }.BeginWrite(filePath, options);
                writer.Write(dataset, data);
                writer.Dispose();

                var allocated = writer.Context.FreeSpaceManager.MetadataAllocated;

                output.WriteLine($"{label,-38} measured {measured,9:N0}   allocated {allocated,9:N0}   shortfall {allocated - measured,9:N0}");

                return allocated - measured;
            }

            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        var ints = Enumerable.Range(0, 1 << 16).ToArray();

        var filtered = Shortfall(
            "filtered chunked int (fixed array)",
            () => new H5Dataset<int[]>(fileDims: [(ulong)ints.Length], chunks: [4096], datasetCreation: new(Filters: [DeflateFilter.Id])),
            ints);

        var unfiltered = Shortfall(
            "unfiltered chunked int (implicit index)",
            () => new H5Dataset<int[]>(fileDims: [(ulong)ints.Length], chunks: [4096]),
            ints);

        var strings = Enumerable.Range(0, 2_000).Select(i => new string('x', 120) + i).ToArray();

        var variableLength = Shortfall(
            "contiguous strings (global heap)",
            () => new H5Dataset<string[]>(fileDims: [(ulong)strings.Length]),
            strings);

        // A chunk index is fully accounted for, however the chunks are indexed.
        Assert.Equal(0, filtered);
        Assert.Equal(0, unfiltered);

        // And so is variable-length data, now that its heap collections are payload. This was 294,912
        // bytes short while they counted as structure.
        Assert.Equal(0, variableLength);
    }

    /// <summary>
    /// A small file must not pay a whole metadata block.
    /// </summary>
    /// <remarks>
    /// A block is claimed in full, so a block of fixed size is a floor on file size rather than a
    /// proportional cost: at a fixed 8 MB, the 33 kB of metadata this file needs makes an 8.4 MB file -
    /// 205 times the interleaved layout. Blocks therefore start small and double, which bounds
    /// what is claimed at roughly twice what is used.
    /// <para>
    /// Deferred variable-length data is the case that provokes it hardest, and under every placement:
    /// aggregation opens its first block, and a front-loaded reservation cannot have measured the global
    /// heap these strings need, so it exhausts and spills into a block too.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void ASmallFileDoesNotPayAWholeMetadataBlock(H5MetadataPlacement placement)
    {
        var strings = Enumerable.Range(0, 500).Select(i => $"deferred-{i}-" + new string('y', i % 61)).ToArray();

        long Write(H5MetadataPlacement value)
        {
            var dataset = new H5Dataset<string[]>(fileDims: [(ulong)strings.Length]);
            var file = new H5File { ["strings"] = dataset };
            var filePath = Path.GetTempFileName();

            try
            {
                using (var writer = file.BeginWrite(filePath, new H5WriteOptions
                {
                    PreferCompactDatasetLayout = false,
                    MetadataPlacement = value

                    // MetadataBlockSize left at its 8 MB default: the ceiling a block claimed whole would pay in full.
                }))
                {
                    writer.Write(dataset, strings);
                }

                return new FileInfo(filePath).Length;
            }

            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        var interleaved = Write(H5MetadataPlacement.Interleaved);
        var clustered = Write(placement);

        output.WriteLine($"interleaved {interleaved,10:N0} bytes   {placement,-12} {clustered,10:N0} bytes   {clustered / (double)interleaved,6:N2}x");

        // A small multiple, not a small difference: some overhead is inherent to claiming space up
        // front. What must not happen is a block of fixed size dwarfing the file, which would be 205x here.
        Assert.True(
            clustered < interleaved * 4,
            $"{placement} produced {clustered:N0} bytes against {interleaved:N0} interleaved, so a block is being claimed whole.");
    }

    /// <summary>
    /// The abandoned figure has to account for the space the file actually grew by.
    /// </summary>
    /// <remarks>
    /// A region is claimed in full and there is no free list, so file size over the interleaved layout
    /// is exactly what was claimed and not used, across every region. That makes the two independently
    /// measurable, which is
    /// the only reason the metric can be checked at all rather than merely reported: growth is observed
    /// from outside, abandonment is reported from inside, and they have to agree.
    /// <para>
    /// Counting only regions already replaced would not agree. A region is replaced only when a request
    /// does not fit it, so those remainders are small by construction - 48 bytes against 8 MB of growth
    /// here - while the tail of the region left open is the part that is actually large.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(H5MetadataPlacement.Aggregated, 0L)]
    [InlineData(H5MetadataPlacement.FrontLoaded, 256 * 1024L)]
    public void AbandonedSpaceAccountsForTheFileGrowth(H5MetadataPlacement placement, long reservation)
    {
        var strings = Enumerable.Range(0, 500).Select(i => $"deferred-{i}-" + new string('y', i % 61)).ToArray();

        (long Size, long Abandoned) Write(H5MetadataPlacement value)
        {
            var dataset = new H5Dataset<string[]>(fileDims: [(ulong)strings.Length]);
            var file = new H5File { ["strings"] = dataset };
            var filePath = Path.GetTempFileName();

            try
            {
                var writer = file.BeginWrite(filePath, new H5WriteOptions
                {
                    PreferCompactDatasetLayout = false,
                    MetadataPlacement = value,

                    // Explicit and deliberately generous for the front-loaded case. Left at zero, the
                    // reservation is exactly what the structure needs and abandons nothing, so there would
                    // be no waste for the metric to account for and the assertion below would hold for the
                    // wrong reason.
                    MetadataReservation = reservation
                });

                writer.Write(dataset, strings);
                writer.Dispose();

                var fsm = writer.Context.FreeSpaceManager;

                return (new FileInfo(filePath).Length, fsm.MetadataAbandoned + fsm.PayloadAbandoned);
            }

            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        var (interleavedSize, interleavedAbandoned) = Write(H5MetadataPlacement.Interleaved);
        var (size, abandoned) = Write(placement);
        var growth = size - interleavedSize;

        output.WriteLine($"{placement,-12} grew {growth,10:N0} bytes, reports {abandoned,10:N0} abandoned");

        // Interleaving claims no region, so it can abandon nothing.
        Assert.Equal(0, interleavedAbandoned);

        // Exactly the growth, not approximately, and summed over both clustered regions: structure and
        // deferred payload are gathered independently, so a file carrying a vlen dataset abandons a tail
        // in each. Interleaving claims precisely what it uses, so the same content written with regions
        // claims those same allocations plus whatever they abandoned. Anything other than equality means
        // space is being claimed that nothing accounts for.
        Assert.True(growth > 0, "this fixture no longer abandons anything, so it proves nothing");
        Assert.Equal(growth, abandoned);
    }

    /// <summary>
    /// Options that cannot mean anything are rejected rather than quietly ignored.
    /// </summary>
    /// <remarks>
    /// Accepted, a non-positive block size would turn <see cref="H5MetadataPlacement.Aggregated" /> into
    /// <see cref="H5MetadataPlacement.Interleaved" /> without a word: the block path is gated on a
    /// positive size, so a caller asking for clustering would get none and nothing to tell them why.
    /// Interleaved ignores the setting, because it does not use blocks.
    /// </remarks>
    [Theory]
    [InlineData(H5MetadataPlacement.Aggregated, 0L, 0L)]
    [InlineData(H5MetadataPlacement.Aggregated, -1L, 0L)]
    [InlineData(H5MetadataPlacement.FrontLoaded, 0L, 0L)]
    [InlineData(H5MetadataPlacement.FrontLoaded, 8 * 1024 * 1024L, -1L)]
    [InlineData(H5MetadataPlacement.Interleaved, 8 * 1024 * 1024L, -1L)]
    public void DegeneratePlacementOptionsAreRejected(
        H5MetadataPlacement placement,
        long blockSize,
        long reservation)
    {
        var file = new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 64).ToArray()) };

        Assert.ThrowsAny<Exception>(() => file.Write(new MemoryStream(), new H5WriteOptions
        {
            MetadataPlacement = placement,
            MetadataBlockSize = blockSize,
            MetadataReservation = reservation
        }));
    }

    /// <summary>
    /// A block size interleaving never consults is not an error.
    /// </summary>
    [Fact]
    public void InterleavingIgnoresTheBlockSize()
    {
        var file = new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 64).ToArray()) };
        var stream = new MemoryStream();

        file.Write(stream, new H5WriteOptions
        {
            MetadataPlacement = H5MetadataPlacement.Interleaved,
            MetadataBlockSize = 0
        });

        stream.Position = 0;

        using var root = H5File.Open(stream, leaveOpen: true);
        Assert.Equal(Enumerable.Range(0, 64).ToArray(), root.Dataset("d").Read<int[]>());
    }

    /// <summary>
    /// A dataset's variable-length values are its payload; an attribute's are structure.
    /// </summary>
    /// <remarks>
    /// Both live on the global heap, so one classification for the whole heap has to be wrong for one of
    /// them. Counted all as structure, a dataset of variable-length strings with no attributes at all
    /// measures 97% structure, so a front-loaded region swallows the payload and there is nothing left to
    /// separate - silently, since the file stays valid and the same size and only the locality quietly
    /// fails to materialise.
    /// <para>
    /// The distinction is the reader's, as everywhere else here: an attribute's value is read while
    /// browsing a file, a dataset's elements only when reading that dataset.
    /// </para>
    /// </remarks>
    [Fact]
    public void ADatasetsVariableLengthValuesArePayloadAndAnAttributesAreStructure()
    {
        var options = new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.Interleaved
        };

        var strings = Enumerable.Range(0, 2_000).Select(i => new string('x', 500) + i).ToArray();

        double StructureShare(string label, Func<H5File> makeFile)
        {
            var measured = H5NativeWriter.MeasureMetadataSize(makeFile(), options);
            var filePath = Path.GetTempFileName();

            try
            {
                makeFile().Write(filePath, options);

                var size = new FileInfo(filePath).Length;
                var share = measured / (double)size;

                output.WriteLine($"{label,-38} structure {measured,10:N0} of {size,10:N0} = {share,7:P1}");

                return share;
            }

            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        var datasetOnly = StructureShare(
            "vlen strings as dataset, no attributes",
            () => new H5File { ["d"] = new H5Dataset(strings) });

        var jagged = StructureShare(
            "jagged arrays as dataset, no attributes",
            () => new H5File { ["d"] = new H5Dataset(Enumerable.Range(0, 2_000).Select(i => Enumerable.Range(0, 125).ToArray()).ToArray()) });

        var attributeOnly = StructureShare(
            "one large string as an attribute",
            () => new H5File { ["g"] = new H5Group { Attributes = new Dictionary<string, object> { ["h"] = string.Join(",", strings.Take(200)) } } });

        // A dataset's heap is payload: what remains as structure is the object headers and the
        // superblock, a rounding error against a megabyte of strings. Counted as structure, it is 97.3%.
        Assert.True(datasetOnly < 0.01, $"a variable-length dataset measured {datasetOnly:P1} structure.");
        Assert.True(jagged < 0.01, $"a jagged-array dataset measured {jagged:P1} structure.");

        // An attribute's heap is structure, and an attributes-only file is therefore all structure.
        Assert.True(attributeOnly > 0.99, $"an attribute-only file measured {attributeOnly:P1} structure.");
    }

    /// <summary>
    /// An unfiltered chunked dataset's payload is payload, however the format reaches it.
    /// </summary>
    /// <remarks>
    /// Such a dataset is indexed implicitly, which means it has no index structure at all: the chunks
    /// sit contiguously from the address in the layout message, and a chunk's address is arithmetic on
    /// it. So what the layout allocates there is the entire payload. Counting it as structure front-loads
    /// the payload along with everything else and dissolves the separation this feature exists to make -
    /// silently, because a reader still finds the file correct and the size never changes.
    /// </remarks>
    [Fact]
    public void AnImplicitChunkIndexIsPayloadRatherThanStructure()
    {
        const int elementCount = 1 << 16;
        const long payload = elementCount * sizeof(int);

        var file = new H5File
        {
            ["chunked"] = new H5Dataset(Enumerable.Range(0, elementCount).ToArray(), chunks: [4096])
        };

        var measured = H5NativeWriter.MeasureMetadataSize(file, new H5WriteOptions
        {
            PreferCompactDatasetLayout = false,
            MetadataPlacement = H5MetadataPlacement.Interleaved
        });

        output.WriteLine($"payload {payload,10:N0} bytes, structure {measured,10:N0} bytes");

        Assert.True(
            measured < payload / 100,
            $"structure measured {measured:N0} bytes against {payload:N0} bytes of payload, so the payload is being counted as structure.");
    }


    [Fact]
    public void MeasureBaseline()
    {
        const int slotSize = 1024 * 1024;

        // Options() rather than the default: the class remark above explains why compact layout has to
        // be off for these files to measure anything.
        var shared = Write(_ => _sharedHint, payloadPerGroup: 2_000, Options());
        var distinct = Write(TypeHint, payloadPerGroup: 2_000, Options());

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

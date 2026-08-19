using System.IO.MemoryMappedFiles;
using HDF.PInvoke;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Reading;

// CONCURRENCY MODEL: a driver instance is owned by one logical reader (its cursor is a plain field,
// because a ThreadLocal<long> reads back as 0 once an async continuation resumes on another thread).
// Concurrency is therefore provided per *read operation*, not per reader: NativeDataset.Read and
// NativeAttribute.Read each allocate a driver over the same file handle / memory-mapped accessor for
// the duration of the operation. So a single H5File can serve many threads at once.
//
// A source qualifies only if it can be read WITHOUT a cursor, so that a second driver over it is
// correct: a file handle (RandomAccess.Read by offset), a memory-mapped accessor (absolute
// addressing), or a Stream that implements IConcurrentStream, whose reads each carry their own offset.
// A plain Stream has one cursor and does not qualify.
//
// Object navigation - root.Group(), root.Dataset(), Children(), Attributes(), LinkExists() - takes a
// scope of its own per public call, so it is concurrent too. The Read-only tests below still resolve
// once on the calling thread (that is the shape they were written for and it is still the common
// usage); the NAVIGATION tests at the bottom of this file are the ones that resolve in parallel.
[Collection(SharedHdf5StateCollection.Name)]
public class ConcurrencyTests
{
    private const int CHUNK_SIZE = 1_000_000;

    // How many of the 1000 attributes CanResolveAttributesByNameParallel_Navigation resolves
    // concurrently. Deliberately below 450, because TestUtils.AddMass replaces that one name with a
    // UTF-8 name.
    private const int NAVIGATION_COUNT = 256;

    // Datasets built by AddManyDatasets. Far past the 8-link compact threshold, so a V18+ group
    // stores these links densely (fractal heap + b-tree v2).
    private const int DATASET_COUNT = 512;

    [Fact]
    public void CanReadDatasetParallel_File_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        // Act
        using var root = NativeFile.InternalOpen(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            deleteOnClose: true);

        // resolved once, on this thread - this test covers concurrent READS (see the note above)
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });
    }

    [Fact]
    public void CanReadDatasetParallel_MMF_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);

        // Act
        using var mmf = MemoryMappedFile.CreateFromFile(filePath);
        using var accessor = mmf.CreateViewAccessor();
        using var root = H5File.Open(accessor);

        // resolved once, on this thread - this test covers concurrent READS (see the note above)
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });
    }

    [Fact]
    public void CanReadDatasetParallel_InMemoryBuffer_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);
        var fileBytes = ReadAllBytesAndDelete(filePath);

        // Act
        using var root = H5File.Open((ReadOnlyMemory<byte>)fileBytes);

        // resolved once, on this thread - this test covers concurrent READS (see the note above)
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });
    }

    // Variable-length data is the case the fixed-size tests above cannot reach, and the one that was
    // silently racy even before the async conversion: the element bytes hold a global-heap ID, so the
    // decoder calls NativeCache.GetGlobalHeapObject, which SEEKS AND READS the driver in the middle
    // of the dataset read (saving and restoring the cursor around it) and populates a process-wide
    // cache. Sharing one driver across threads corrupts both the cursor and the collection decode.
    //
    // Every thread starts on a cold cache, so they collide on the first-miss path, and the assertion
    // is on the decoded strings - a race here yields wrong or truncated values, not an exception.
    [Theory]
    [InlineData(DriverKind.FileHandle)]
    [InlineData(DriverKind.MemoryMappedFile)]
    [InlineData(DriverKind.InMemoryBuffer)]
    public void CanReadVariableLengthDatasetParallel_Threads(DriverKind driverKind)
    {
        // Arrange
        var version = H5F.libver_t.LATEST;

        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddString(fileId, ContainerType.Dataset));

        var expected = new string[]
        {
            "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
        };

        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;
        NativeFile root;

        if (driverKind == DriverKind.MemoryMappedFile)
        {
            mmf = MemoryMappedFile.CreateFromFile(filePath);
            accessor = mmf.CreateViewAccessor();
            root = H5File.Open(accessor);
        }

        else if (driverKind == DriverKind.InMemoryBuffer)
        {
            var fileBytes = ReadAllBytesAndDelete(filePath);
            root = H5File.Open((ReadOnlyMemory<byte>)fileBytes);
        }

        else
        {
            root = NativeFile.InternalOpen(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                deleteOnClose: false);
        }

        try
        {
            // resolved once, on this thread - this test covers concurrent READS (see the note above)
            var dataset = root.Group("string").Dataset("variable");

            // Act
            Parallel.For(0, 64, _ =>
            {
                var actual = dataset.Read<string[]>();

                // Assert
                Assert.Equal(expected.Length, actual.Length);

                for (int j = 0; j < expected.Length; j++)
                {
                    Assert.Equal(expected[j], actual[j]);
                }
            });
        }

        finally
        {
            root.Dispose();
            accessor?.Dispose();
            mmf?.Dispose();

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    // A Stream can now participate too, but only if it offers positionless reads: IConcurrentStream
    // carries an absolute offset per read, so the driver keeps the cursor to itself and can hand a
    // second driver - with its own cursor - to each read operation. A plain Stream still cannot, and
    // there is no test asserting it can; it has exactly one cursor to share.
    [Fact]
    public void CanReadDatasetParallel_DatasetStream_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddChunkedDataset_Huge);
        var fileBytes = ReadAllBytesAndDelete(filePath);

        // Act
        using var stream = new ConcurrentStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);

        // resolved once, on this thread - this test covers concurrent READS (see the note above)
        var parent = root.Group("chunked");
        var dataset = parent.Dataset("chunked_huge");

        Parallel.For(0, 10, i =>
        {
            var fileSelection = new HyperslabSelection(
                start: (uint)i * CHUNK_SIZE,
                block: CHUNK_SIZE
            );

            var actual = dataset.Read<int[]>(fileSelection);

            // Assert
            var slicedData = SharedTestData.HugeData.AsSpan(i * CHUNK_SIZE, CHUNK_SIZE).ToArray();
            Assert.True(actual.SequenceEqual(slicedData));
        });

        // Assert: both halves of IConcurrentStream were actually exercised. The split is the reason the
        // interface has two methods - an implementation caches metadata and streams payload - so a
        // change that routed structural reads through ReadDataset would still decode correct bytes
        // and destroy the signal. This catches that.
        Assert.True(stream.DatasetReadCount > 0);
        Assert.True(stream.MetadataReadCount > 0);
    }

    // The variable-length counterpart, for the same reason as the driver cases above: the element
    // bytes hold a global-heap ID, so decoding seeks and reads the driver mid-read and populates a
    // per-file cache.
    [Fact]
    public void CanReadVariableLengthDatasetParallel_DatasetStream_Threads()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;

        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddString(fileId, ContainerType.Dataset));

        var fileBytes = ReadAllBytesAndDelete(filePath);

        var expected = new string[]
        {
            "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
        };

        // Act
        using var stream = new ConcurrentStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);

        // resolved once, on this thread - this test covers concurrent READS (see the note above)
        var dataset = root.Group("string").Dataset("variable");

        Parallel.For(0, 64, _ =>
        {
            var actual = dataset.Read<string[]>();

            // Assert
            Assert.Equal(expected.Length, actual.Length);

            for (int j = 0; j < expected.Length; j++)
            {
                Assert.Equal(expected[j], actual[j]);
            }
        });
    }

    // Not a concurrency test: it covers the other half of positionless mode, a stream that genuinely
    // suspends. Every read here completes asynchronously, so the driver takes its async continuations
    // (ReadScalarSlow, the ReadBytes continuation) on every single read instead of the synchronous
    // fast path - which is where a cursor advanced in the wrong place would show up as garbage.
    //
    // Deliberately single-threaded. The public read API is synchronous and blocks on the ValueTask,
    // so many threads each blocking on a continuation that itself needs a thread-pool thread is a
    // starvation pattern, not a useful test.
    [Fact]
    public void CanReadSuspendingDatasetStream()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;

        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddString(fileId, ContainerType.Dataset));

        var fileBytes = ReadAllBytesAndDelete(filePath);

        var expected = new string[]
        {
            "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"
        };

        // Act
        using var stream = new ConcurrentStream(fileBytes, suspend: true);
        using var root = H5File.Open(stream);

        var actual = root.Group("string").Dataset("variable").Read<string[]>();

        // Assert
        Assert.Equal(expected, actual);
    }

    // NAVIGATION. Everything above resolves an object on the calling thread and only reads in
    // parallel. These resolve in parallel, which is the case that moves the ONE file-level cursor from
    // several threads at once unless each operation is isolated: Dereference / NativeObject.Header
    // seek then read, so an
    // interleaving lands a header decode on another thread's offset. It shows up as a signature or
    // checksum FormatException, an absurd allocation, or - worst - a plausible wrong answer.
    //
    // The tests are parameterised over the library version on purpose, because the group and
    // attribute storage forms are DIFFERENT code paths through the types this change touched, and a
    // fix for one does not imply a fix for the other:
    //
    //   EARLIEST  old-style groups - ObjectHeaderScratchPad / SymbolTableMessage, reached through a
    //             b-tree v1 and a local heap. (Verified by instrumenting both branches: a path walk
    //             from the root takes SymbolTableMessage for the root itself and the scratch-pad
    //             branch for the group found in it, so one walk covers both.)
    //   V18/V110  "dense" storage - LinkInfoMessage / AttributeInfoMessage, reached through a fractal
    //             heap and a b-tree v2. 1000 links/attributes is far past the compact threshold.
    //
    // None of those four message types holds a context or caches the heap or b-tree it decodes; an
    // unsynchronised lazy field keyed on a context captured at decode time is exactly the hazard here.
    // So these tests also cover the fresh-decode-per-call path.

    [Theory]
    [InlineData(DriverKind.FileHandle, H5F.libver_t.EARLIEST)]
    [InlineData(DriverKind.FileHandle, H5F.libver_t.V110)]
    [InlineData(DriverKind.MemoryMappedFile, H5F.libver_t.V110)]
    public void CanResolveDatasetsByNameParallel_Navigation(DriverKind driverKind, H5F.libver_t version)
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(version, AddManyDatasets);

        // Act
        using var handle = new RootHandle(driverKind, filePath);
        var root = handle.Root;

        // (a) two-segment path walk from the shared file, resolved concurrently. Every iteration
        //     dereferences the intermediate group again, so the walk itself is under contention.
        Parallel.For(0, DATASET_COUNT, i =>
        {
            var name = $"nav_{i:D4}";
            var dataset = root.Dataset($"navigation/{name}");

            // Assert - the NAME proves the right link was resolved, not merely that some header
            // decoded, and the DISTINCT payload proves it independently of the name.
            Assert.Equal(name, dataset.Name);
            Assert.Equal(ExpectedData(i), dataset.Read<int[]>());
        });

        // (b) same, but against one shared NativeGroup rather than the file - a single-segment
        //     lookup straight into the group's link storage, plus LinkExists on the same group.
        var parent = root.Group("navigation");

        Parallel.For(0, DATASET_COUNT, i =>
        {
            var name = $"nav_{i:D4}";

            Assert.True(parent.LinkExists(name));

            var dataset = parent.Dataset(name);

            Assert.Equal(name, dataset.Name);
            Assert.Equal(ExpectedData(i), dataset.Read<int[]>());
        });
    }

    [Theory]
    [InlineData(H5F.libver_t.EARLIEST)]
    [InlineData(H5F.libver_t.V110)]
    public void CanEnumerateChildrenParallel_Navigation(H5F.libver_t version)
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(version, TestUtils.AddMassLinks);

        var expected = Enumerable
            .Range(0, 1000)
            .Select(i => $"mass_{i:D4}")
            .ToArray();

        // Act
        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var group = root.Group("mass_links");

        // Each Children() enumeration walks the whole link storage and dereferences all 1000 children.
        // They run concurrently against ONE shared NativeGroup, so every enumeration must get its own
        // driver - a per-enumeration scope, not a per-group one.
        Parallel.For(0, 8, _ =>
        {
            var actual = group
                .Children()
                .Select(child => child.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            // Assert
            Assert.Equal(expected, actual);
        });

        // The root group is reached WITHOUT a symbol table entry, so on an EARLIEST file its links are
        // read through its own SymbolTableMessage rather than through an ObjectHeaderScratchPad. That
        // is a separate accessor pair on a separate type, and enumerating `group` above never reaches
        // it - verified by instrumenting both branches.
        Parallel.For(0, 8, _ =>
        {
            var actual = root
                .Children()
                .Select(child => child.Name)
                .ToArray();

            // Assert
            Assert.Equal(["mass_links"], actual);
        });
    }

    // Dense attributes are the AttributeInfoMessage half of the same problem: the fractal heap and
    // b-tree v2 are decoded per call rather than cached on the message, and both the by-name lookup
    // and the enumeration drive them. EARLIEST is not covered here because it has no dense storage
    // at all - 1000 attributes cannot fit in a 64 KB object header - so V18 and V110 are the two
    // versions that exercise this path.
    [Theory]
    [InlineData(H5F.libver_t.V18)]
    [InlineData(H5F.libver_t.V110)]
    public void CanResolveAttributesByNameParallel_Navigation(H5F.libver_t version)
    {
        // Arrange
        var filePath = TestUtils.PrepareTestFile(version, fileId
            => TestUtils.AddMass(fileId, ContainerType.Attribute));

        var expected = ReadingTestData.NonNullableStructData;

        // Act
        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var parent = root.Group("mass_attributes");

        Parallel.For(0, NAVIGATION_COUNT, i =>
        {
            var name = $"mass_{i:D4}";

            Assert.True(parent.AttributeExists(name));

            var attribute = parent.Attribute(name);

            // Assert
            Assert.Equal(name, attribute.Name);
            Assert.True(expected.SequenceEqual(attribute.Read<TestStructL1[]>()));
        });

        Parallel.For(0, 8, _ =>
        {
            var attributes = parent.Attributes().ToList();

            // Assert
            Assert.Equal(1000, attributes.Count);
        });
    }

    // TestUtils has no many-DATASETS helper. TestUtils.AddMass builds 1000 elements in one group, but
    // one of them is created with an H5P.ATTRIBUTE_CREATE property list, which H5D.create rejects
    // ("Could not create dataset") - so it only works for ContainerType.Attribute, and
    // CanResolveAttributesByNameParallel_Navigation above uses it for exactly that. This builds the
    // dataset equivalent from the same TestUtils.Add primitive and, unlike AddMass, gives every
    // dataset a DISTINCT payload, so resolving the wrong link is caught by value and not only by name.
    private static void AddManyDatasets(long fileId)
    {
        for (int i = 0; i < DATASET_COUNT; i++)
        {
            var data = ExpectedData(i);

            TestUtils.Add(
                ContainerType.Dataset,
                fileId,
                "navigation",
                $"nav_{i:D4}",
                H5T.NATIVE_INT32,
                data.AsSpan());
        }
    }

    private static int[] ExpectedData(int index)
    {
        return [index, index + 1, index + 2, index + 3];
    }

    private static byte[] ReadAllBytesAndDelete(string filePath)
    {
        try
        {
            return File.ReadAllBytes(filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    public enum DriverKind
    {
        FileHandle,
        MemoryMappedFile,
        InMemoryBuffer
    }

    /// <summary>
    /// Opens a test file through the requested driver and cleans up the file afterwards.
    /// </summary>
    /// <remarks>
    /// The memory-mapped case needs the accessor and the mapping torn down in order, and after the
    /// <see cref="NativeFile" />, which is why this is not a plain <c>using</c> at the call site.
    /// </remarks>
    private sealed class RootHandle : IDisposable
    {
        private readonly MemoryMappedFile? _mmf;
        private readonly MemoryMappedViewAccessor? _accessor;
        private readonly string _filePath;

        public RootHandle(DriverKind driverKind, string filePath)
        {
            _filePath = filePath;

            if (driverKind == DriverKind.MemoryMappedFile)
            {
                _mmf = MemoryMappedFile.CreateFromFile(filePath);
                _accessor = _mmf.CreateViewAccessor();
                Root = H5File.Open(_accessor);
            }

            else
            {
                Root = NativeFile.InternalOpen(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    deleteOnClose: false);
            }
        }

        public NativeFile Root { get; }

        public void Dispose()
        {
            Root.Dispose();
            _accessor?.Dispose();
            _mmf?.Dispose();

            if (File.Exists(_filePath))
                File.Delete(_filePath);
        }
    }
}

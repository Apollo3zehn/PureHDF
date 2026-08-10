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
// addressing), or a Stream that implements IDatasetStream, whose reads each carry their own offset.
// A plain Stream has one cursor and does not qualify.
//
// BOUNDARY - what these tests deliberately do NOT do concurrently: object navigation. root.Group(),
// root.Dataset(), attribute enumeration and anything else that walks the file structure moves the
// FILE-LEVEL driver cursor and has no per-operation driver of its own. That is why every test below
// resolves the dataset ONCE, on the calling thread, and only the Read calls run in parallel. Moving
// a `.Dataset(...)` call inside a Parallel.For here would be testing an unsupported usage.
[Collection(SharedHdf5StateCollection.Name)]
public class ConcurrencyTests
{
    private const int CHUNK_SIZE = 1_000_000;

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

        // resolved once, on this thread - see the boundary note above
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

        // resolved once, on this thread - see the boundary note above
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
            // resolved once, on this thread - see the boundary note above
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

    // A Stream can now participate too, but only if it offers positionless reads: IDatasetStream
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
        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);

        // resolved once, on this thread - see the boundary note above
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

        // Assert: both halves of IDatasetStream were actually exercised. The split is the reason the
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
        using var stream = new PositionlessDatasetStream(fileBytes, suspend: false);
        using var root = H5File.Open(stream);

        // resolved once, on this thread - see the boundary note above
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
        using var stream = new PositionlessDatasetStream(fileBytes, suspend: true);
        using var root = H5File.Open(stream);

        var actual = root.Group("string").Dataset("variable").Read<string[]>();

        // Assert
        Assert.Equal(expected, actual);
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
        MemoryMappedFile
    }

    /// <summary>
    /// An <see cref="IDatasetStream" /> over an in-memory buffer.
    /// </summary>
    /// <remarks>
    /// A positionless read out of a <c>byte[]</c> is inherently safe to perform from many threads at
    /// once, so this contains no synchronization at all beyond the two call counters - which means
    /// anything that goes wrong in the tests above is the driver sharing a cursor, not the stream.
    /// <para>
    /// The cursor-based <see cref="Stream" /> members throw. PureHDF must not reach for them once
    /// <see cref="IDatasetStream" /> is implemented - that is the whole point - so any read or seek
    /// still going through the stream cursor fails the test loudly instead of quietly working on this
    /// one thread and racing on any other.
    /// </para>
    /// </remarks>
    private sealed class PositionlessDatasetStream : Stream, IDatasetStream
    {
        private readonly byte[] _data;
        private readonly bool _suspend;
        private int _datasetReadCount;
        private int _metadataReadCount;

        public PositionlessDatasetStream(byte[] data, bool suspend)
        {
            _data = data;
            _suspend = suspend;
        }

        public int DatasetReadCount => Volatile.Read(ref _datasetReadCount);

        public int MetadataReadCount => Volatile.Read(ref _metadataReadCount);

        public ValueTask ReadDataset(long offset, Memory<byte> buffer)
        {
            Interlocked.Increment(ref _datasetReadCount);

            return ReadCore(offset, buffer);
        }

        public ValueTask ReadMetadata(long offset, Memory<byte> buffer)
        {
            Interlocked.Increment(ref _metadataReadCount);

            return ReadCore(offset, buffer);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => throw CursorUsed();
            set => throw CursorUsed();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw CursorUsed();

        public override long Seek(long offset, SeekOrigin origin) => throw CursorUsed();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private ValueTask ReadCore(long offset, Memory<byte> buffer)
        {
            if (_suspend)
                return SuspendThenCopy(offset, buffer);

            Copy(offset, buffer);

            return default;
        }

        private async ValueTask SuspendThenCopy(long offset, Memory<byte> buffer)
        {
            await Task.Run(() => Copy(offset, buffer)).ConfigureAwait(false);
        }

        private void Copy(long offset, Memory<byte> buffer)
        {
            if (offset < 0 || offset + buffer.Length > _data.Length)
                throw new EndOfStreamException($"Read of {buffer.Length} bytes at offset {offset} exceeds the {_data.Length} byte buffer.");

            _data.AsSpan((int)offset, buffer.Length).CopyTo(buffer.Span);
        }

        private static InvalidOperationException CursorUsed()
        {
            return new InvalidOperationException(
                "A cursor-based Stream member was used although IDatasetStream is implemented.");
        }
    }
}

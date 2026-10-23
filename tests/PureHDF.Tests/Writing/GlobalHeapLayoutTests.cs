using System.Buffers.Binary;
using System.Text;
using PureHDF.Selections;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     Where and how the writer places global heap collections - the storage behind every variable-length value.
/// </summary>
/// <remarks>
///     Assertions read the collections straight out of the written bytes: each starts with "GCOL", a version
///     byte, three reserved bytes and its size, so the file can be inspected without a reader that would hide
///     the layout. Values are kept free of that signature so that only collections match it.
/// </remarks>
public class GlobalHeapLayoutTests
{
    private const int CollectionHeaderSize = 16;
    private const int ObjectHeaderSize = 16;
    private const int MinimumCollectionSize = 4096;

    [Fact]
    public void ALargeValueDoesNotEndTheCollectionSmallValuesAreFilling()
    {
        // Arrange - small values, one too large for any 4 kB collection, then small values again
        var values = Enumerable.Range(0, 20).Select(i => $"small-{i}")
            .Append(new string('x', 6000))
            .Concat(Enumerable.Range(20, 20).Select(i => $"small-{i}"))
            .ToArray();

        var dataset = new H5Dataset(1);

        for (var i = 0; i < values.Length; i++)
            dataset.Attributes[$"attribute-{i:D2}"] = values[i];

        var file = new H5File { ["dataset"] = dataset };

        // Act
        var bytes = Write(file, new H5WriteOptions { AttributeStringLength = H5AttributeStringLength.VariableLength });

        // Assert - the small values share one collection; the large one has its own
        Assert.Equal(2, Collections(bytes).Count);
        AssertReadable(bytes);
    }

    [Fact]
    public void AVariableLengthDatasetWriteGetsOneCollectionSizedToItsValues()
    {
        // Arrange
        var data = Enumerable.Range(0, 10_000).Select(i => $"value-{i}").ToArray();
        var file = new H5File { ["strings"] = new H5Dataset(data) };

        // Act
        var bytes = Write(file, new H5WriteOptions());

        // Assert
        var collection = Assert.Single(Collections(bytes));
        Assert.Equal((ulong)ExactCollectionSize(data), collection.Size);
        AssertReadable(bytes, "/strings", data);
    }

    [Fact]
    public void AVariableLengthDatasetWriteBeyondTheObjectLimitGetsAdjacentCollections()
    {
        // Arrange - more values than one collection can index
        var data = Enumerable.Range(0, 70_000).Select(i => $"v{i}").ToArray();
        var file = new H5File { ["strings"] = new H5Dataset(data) };

        // Act
        var bytes = Write(file, new H5WriteOptions());

        // Assert - two collections, the second starting where the first ends
        var collections = Collections(bytes);

        Assert.Equal(2, collections.Count);
        Assert.Equal(collections[0].Offset + (long)collections[0].Size, collections[1].Offset);
        AssertReadable(bytes, "/strings", data);
    }

    [Fact]
    public void SmallVariableLengthWritesShareACollection()
    {
        // Arrange - each far below the 4 kB a collection takes at least
        var a = new[] { "one", "two", "three" };
        var b = new[] { "four", "five" };

        var file = new H5File
        {
            ["a"] = new H5Dataset(a),
            ["b"] = new H5Dataset(b)
        };

        // Act
        var bytes = Write(file, new H5WriteOptions());

        // Assert
        Assert.Single(Collections(bytes));
        AssertReadable(bytes, "/a", a);
        AssertReadable(bytes, "/b", b);
    }

    [Fact]
    public void ADeferredVariableLengthWriteIsWrittenBeforeTheWriterIsDisposed()
    {
        // Arrange
        var data = Enumerable.Range(0, 1_000).Select(i => $"deferred-{i}").ToArray();
        var dataset = new H5Dataset<string[]>(fileDims: [(ulong)data.Length]);
        var file = new H5File { ["strings"] = dataset };
        var stream = new MemoryStream();

        // Act
        using (var writer = file.BeginWrite(stream, new H5WriteOptions()))
        {
            writer.Write(dataset, data);

            // Assert - the heap is in the stream already, rather than waiting for Dispose
            Assert.Single(Collections(stream.ToArray()));
        }

        AssertReadable(stream.ToArray(), "/strings", data);
    }

    [Fact]
    public void AfterEachWriteOnlyTheCollectionStillFillingIsLeftUnwritten()
    {
        // Arrange - writes too small for a heap of their own, sharing collections
        var dataset = new H5Dataset<string[]>(fileDims: [2_000]);
        var file = new H5File { ["strings"] = dataset };
        var stream = new MemoryStream();
        var data = Enumerable.Range(0, 2_000).Select(i => $"small-{i}").ToArray();

        // Act
        using (var writer = file.BeginWrite(stream, new H5WriteOptions()))
        {
            for (var start = 0; start < data.Length; start += 20)
            {
                writer.Write(dataset, data[start..(start + 20)], fileSelection: new HyperslabSelection((ulong)start, 20));

                // Assert - every collection but the one being filled is in the stream already
                var written = Collections(stream.ToArray()).Count;
                var allocated = CollectionsNeeded(data[..(start + 20)]);

                Assert.True(written >= allocated - 1, $"After {start + 20} values only {written} of {allocated} collections were written.");
            }
        }

        AssertReadable(stream.ToArray(), "/strings", data);
    }

    [Fact]
    public void AttributeValuesAreWrittenBeforeBeginWriteReturns()
    {
        // Arrange
        var dataset = new H5Dataset<int[]>(fileDims: [3]);
        dataset.Attributes["unit"] = "volt";

        var file = new H5File { ["values"] = dataset };
        var stream = new MemoryStream();

        // Act
        using (var writer = file.BeginWrite(stream, new H5WriteOptions { AttributeStringLength = H5AttributeStringLength.VariableLength }))
        {
            // Assert - written with the structure, not left for Dispose to write back into it
            Assert.Single(Collections(stream.ToArray()));

            writer.Write(dataset, [1, 2, 3]);
        }

        AssertReadable(stream.ToArray(), "/values", new[] { 1, 2, 3 });
    }

    [Fact]
    public void NestedVariableLengthValuesSpanningSeveralCollectionsReadBackWhole()
    {
        // Arrange - a sequence of strings per element: the outer value is still being filled while the
        // inner ones are added
        var data = Enumerable.Range(0, 3_000)
            .Select(i => Enumerable.Range(0, i % 5).Select(j => $"{i}.{j}").ToArray())
            .ToArray();

        var file = new H5File { ["nested"] = new H5Dataset(data) };

        // Act
        var bytes = Write(file, new H5WriteOptions());

        // Assert
        AssertReadable(bytes);

        using var actual = H5File.Open(new MemoryStream(bytes));
        var read = actual.Dataset("nested").Read<string[][]>();

        Assert.Equal(data.Length, read.Length);

        for (var i = 0; i < data.Length; i++)
            Assert.Equal(data[i], read[i]);
    }

    private static int ExactCollectionSize(string[] values)
    {
        var objects = values.Sum(value => ObjectHeaderSize + Align8(Encoding.UTF8.GetByteCount(value)));

        // The collection header, every object, and the header of object 0, which records the free space.
        return Math.Max(MinimumCollectionSize, CollectionHeaderSize + objects + ObjectHeaderSize);
    }

    private static int Align8(int size) => (size + 7) / 8 * 8;

    // How many minimum-sized collections a run of values packed one after the other fills.
    private static int CollectionsNeeded(string[] values)
    {
        var count = 1;
        var consumed = 0;

        foreach (var value in values)
        {
            var size = ObjectHeaderSize + Align8(Encoding.UTF8.GetByteCount(value));

            if (consumed + size > MinimumCollectionSize - CollectionHeaderSize)
            {
                count++;
                consumed = 0;
            }

            consumed += size;
        }

        return count;
    }

    private static byte[] Write(H5File file, H5WriteOptions options)
    {
        var stream = new MemoryStream();
        file.Write(stream, options);

        return stream.ToArray();
    }

    private static List<(long Offset, ulong Size)> Collections(byte[] bytes)
    {
        var signature = "GCOL"u8;
        var result = new List<(long, ulong)>();

        for (var offset = bytes.AsSpan().IndexOf(signature); offset >= 0;)
        {
            result.Add((offset, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + 8, 8))));

            var next = bytes.AsSpan(offset + 4).IndexOf(signature);
            offset = next < 0 ? -1 : offset + 4 + next;
        }

        return result;
    }

    private static void AssertReadable(byte[] bytes)
    {
        // h5dump reads every value through the C library, which checks each collection it touches.
        var filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(filePath, bytes);
            TestUtils.DumpH5File(filePath);
        }

        finally
        {
            File.Delete(filePath);
        }
    }

    private static void AssertReadable<T>(byte[] bytes, string path, T[] expected)
    {
        AssertReadable(bytes);

        using var actual = H5File.Open(new MemoryStream(bytes));
        Assert.Equal(expected, actual.Dataset(path).Read<T[]>());
    }
}

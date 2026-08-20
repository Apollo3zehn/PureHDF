using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Writing;

/// <summary>
/// Every placement must produce a file that reads back, for the data types whose encoding allocates.
/// </summary>
/// <remarks>
/// Variable-length data is what makes placement interesting and what makes it risky. Its global heap
/// collections are metadata, so they move with the structure, and their size follows from the values
/// rather than from the dataspace - which is the one thing a sizing pass cannot know in advance. So
/// these are the writes where a region can be exhausted mid-file and the allocator has to fall back
/// while addresses already encoded stay correct. A placement that loses locality is a disappointment;
/// one that loses data is a catastrophe, and from a byte count the two look identical.
/// </remarks>
public class MetadataPlacementRoundTripTests(ITestOutputHelper output)
{
    private struct Mixed
    {
        public int Number;
        public string Text;
    }

    private static H5WriteOptions Options(H5MetadataPlacement placement) => new()
    {
        PreferCompactDatasetLayout = false,
        MetadataPlacement = placement
    };

    [Theory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void VariableLengthDataSurvivesEveryPlacement(H5MetadataPlacement placement)
    {
        // Arrange - one dataset per path through the element encoder that touches the global heap.
        var strings = Enumerable.Range(0, 500).Select(i => $"value-{i}-" + new string('x', i % 97)).ToArray();
        var jagged = Enumerable.Range(0, 200).Select(i => Enumerable.Range(0, i % 13 + 1).ToArray()).ToArray();
        var nullables = Enumerable.Range(0, 300).Select(i => i % 5 == 0 ? (int?)null : i).ToArray();
        var mixed = Enumerable.Range(0, 100).Select(i => new Mixed { Number = i, Text = $"row-{i}" }).ToArray();
        var hint = new string('h', 300);

        var file = new H5File
        {
            ["strings"] = new H5Dataset(strings),
            ["jagged"] = new H5Dataset(jagged),
            ["nullables"] = new H5Dataset(nullables),
            ["mixed"] = new H5Dataset(mixed),
            Attributes = new Dictionary<string, object> { ["hint"] = hint }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath, Options(placement));

            output.WriteLine($"{placement,-12} {new FileInfo(filePath).Length,10:N0} bytes");

            // Assert
            using var root = H5File.OpenRead(filePath);

            Assert.Equal(strings, root.Dataset("strings").Read<string[]>());
            Assert.Equal(jagged, root.Dataset("jagged").Read<int[][]>());
            Assert.Equal(nullables, root.Dataset("nullables").Read<int?[]>());
            Assert.Equal(mixed, root.Dataset("mixed").Read<Mixed[]>());
            Assert.Equal(hint, root.Attribute("hint").Read<string>());
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// The same, written after the fact - where the sizing pass could not have seen the values.
    /// </summary>
    /// <remarks>
    /// The case a measured reservation cannot cover, so under a front-loaded placement this write is
    /// guaranteed to exhaust its region and take the fallback path. It has to end in a correct file
    /// regardless, which is the whole reason the fallback opens a new region rather than throwing.
    /// </remarks>
    [Theory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void DeferredVariableLengthDataSurvivesEveryPlacement(H5MetadataPlacement placement)
    {
        // Arrange
        var strings = Enumerable.Range(0, 500).Select(i => $"deferred-{i}-" + new string('y', i % 61)).ToArray();

        var dataset = new H5Dataset<string[]>(fileDims: [(ulong)strings.Length]);
        var file = new H5File { ["strings"] = dataset };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            using (var writer = file.BeginWrite(filePath, Options(placement)))
            {
                writer.Write(dataset, strings);
            }

            output.WriteLine($"{placement,-12} {new FileInfo(filePath).Length,10:N0} bytes");

            // Assert
            using var root = H5File.OpenRead(filePath);

            Assert.Equal(strings, root.Dataset("strings").Read<string[]>());
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}

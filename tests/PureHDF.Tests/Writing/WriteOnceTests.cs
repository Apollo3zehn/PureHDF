using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     Every byte of a written file is written exactly once, and the only bytes not written at all are the
///     unused tails of the blocks still open when the writer is disposed.
/// </summary>
/// <remarks>
///     A destination that uploads a file in parts can ship a part as soon as every byte of it has been
///     written, provided nothing is written into it afterwards and nothing in it is left unwritten for
///     good. Rewriting a part already shipped corrupts the upload; a gap that is never filled holds its
///     part until the end. The unused tail of a block still open at the end is allocated but never named
///     by anything, so the destination can fill it with zeros once the writer is disposed.
/// </remarks>
public class WriteOnceTests
{
    private static readonly Dictionary<string, Func<Scenario>> Scenarios = new()
    {
        ["large group"] = LargeGroup
    };

    public static TheoryData<string, H5MetadataPlacement> Cases()
    {
        var cases = new TheoryData<string, H5MetadataPlacement>();

        foreach (var name in Scenarios.Keys)
        {
            cases.Add(name, H5MetadataPlacement.Aggregated);
            cases.Add(name, H5MetadataPlacement.Interleaved);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NoByteIsWrittenTwice(string scenarioName, H5MetadataPlacement placement)
    {
        // Arrange
        var scenario = Scenarios[scenarioName]();

        // Act
        var (stream, _) = Write(scenario, placement);

        // Assert
        var rewritten = stream.Ranges(count => count > 1);
        Assert.True(rewritten.Count == 0, $"Rewritten (offset, length): {Describe(rewritten)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void OnlyTheTailsOfOpenBlocksAreLeftUnwritten(string scenarioName, H5MetadataPlacement placement)
    {
        // Arrange
        var scenario = Scenarios[scenarioName]();

        // Act
        var (stream, openTails) = Write(scenario, placement);

        // Assert
        var unexpected = stream
            .Ranges(count => count == 0)
            .Where(hole => !openTails.Any(tail => tail.Address <= hole.Offset && hole.Offset + hole.Length <= tail.Address + tail.Length))
            .ToList();

        Assert.True(unexpected.Count == 0, $"Unwritten (offset, length): {Describe(unexpected)}");
    }

    private static (CoverageRecordingStream Stream, List<(long Address, long Length)> OpenTails) Write(
        Scenario scenario, H5MetadataPlacement placement)
    {
        var options = scenario.Options with { MetadataPlacement = placement };
        var stream = new CoverageRecordingStream();
        List<(long, long)> openTails;

        using (var writer = scenario.File.BeginWrite(stream, options))
        {
            foreach (var write in scenario.Writes)
                write(writer);

            openTails = writer.Context.FreeSpaceManager.OpenTails.ToList();
        }

        // The file is valid, and reads back.
        using var file = H5File.Open(new MemoryStream(stream.ToArray()));
        scenario.Verify(file);

        return (stream, openTails);
    }

    private static string Describe(List<(long Offset, long Length)> ranges)
        => string.Join(", ", ranges.Take(20).Select(range => $"({range.Offset}, {range.Length})"));

    private sealed record Scenario(
        H5File File,
        Action<IH5Group> Verify,
        List<Action<H5NativeWriter>>? DeferredWrites = default,
        H5WriteOptions? WriteOptions = default)
    {
        public List<Action<H5NativeWriter>> Writes => DeferredWrites ?? [];

        public H5WriteOptions Options => WriteOptions ?? new H5WriteOptions();
    }

    #region Scenarios

    // A header too large for what is left of a block abandons the rest of it.
    private static Scenario LargeGroup()
    {
        var group = new H5Group();

        for (var i = 0; i < 5_000; i++)
            group[$"dataset_{i:D4}"] = new H5Dataset(i);

        return new(new H5File { ["large"] = group }, file => Assert.Equal(4_999, file.Dataset("large/dataset_4999").Read<int>()));
    }

    #endregion

    /// <summary>
    ///     Counts how often each byte is written.
    /// </summary>
    private sealed class CoverageRecordingStream : MemoryStream
    {
        private byte[] _counts = new byte[64 * 1024];

        public override bool CanRead => false;

        // Stream.Write(ReadOnlySpan<byte>) on a derived MemoryStream routes here, so this is the only
        // write path apart from WriteByte.
        public override void Write(byte[] buffer, int offset, int count)
        {
            Record(Position, count);
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            Record(Position, 1);
            base.WriteByte(value);
        }

        private void Record(long position, int count)
        {
            var end = position + count;

            if (end > _counts.Length)
                Array.Resize(ref _counts, (int)Math.Max(end, _counts.Length * 2L));

            for (var i = position; i < end; i++)
            {
                if (_counts[i] < byte.MaxValue)
                    _counts[i]++;
            }
        }

        public List<(long Offset, long Length)> Ranges(Func<byte, bool> predicate)
        {
            var ranges = new List<(long, long)>();
            var length = Length;
            long? start = null;

            for (long i = 0; i < length; i++)
            {
                var matches = predicate(i < _counts.Length ? _counts[i] : (byte)0);

                if (matches && start is null)
                    start = i;

                else if (!matches && start is not null)
                {
                    ranges.Add((start.Value, i - start.Value));
                    start = null;
                }
            }

            if (start is not null)
                ranges.Add((start.Value, length - start.Value));

            return ranges;
        }
    }
}

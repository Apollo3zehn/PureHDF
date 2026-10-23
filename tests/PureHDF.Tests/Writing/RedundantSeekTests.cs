using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     The writer does not seek a stream to where it already is.
/// </summary>
/// <remarks>
///     A buffered <see cref="FileStream" /> flushes its write buffer on every Seek, including one that does
///     not move, so a seek to the current position costs a write call. Under a clustered placement the
///     structures the writer encodes one after another are allocated back to back, so most of its seeks
///     would be of that kind.
/// </remarks>
public class RedundantSeekTests
{
    [Theory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void NoSeekLandsWhereTheStreamAlreadyIs(H5MetadataPlacement placement)
    {
        // Arrange
        var file = new H5File();

        for (var i = 0; i < 100; i++)
        {
            file[$"group{i}"] = new H5Group
            {
                ["values"] = new H5Dataset(Enumerable.Range(i, 10).ToArray())
            };
        }

        using var stream = new SeekRecordingStream();

        // Act
        file.Write(stream, new H5WriteOptions { MetadataPlacement = placement });

        // Assert
        Assert.True(stream.Seeks > 0, "the writer did not seek at all, so this proves nothing");
        Assert.Equal(0, stream.RedundantSeeks);
    }

    private sealed class SeekRecordingStream : MemoryStream
    {
        public int Seeks { get; private set; }

        public int RedundantSeeks { get; private set; }

        public override long Seek(long offset, SeekOrigin loc)
        {
            Seeks++;

            if (loc == SeekOrigin.Begin && offset == Position)
                RedundantSeeks++;

            return base.Seek(offset, loc);
        }
    }
}

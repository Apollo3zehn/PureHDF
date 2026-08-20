using System.Buffers;
using PureHDF.Filters;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A fixed-length string is padded with zero bytes, whatever the padding length.
/// </summary>
/// <remarks>
///     The padding was written straight from a <see cref="MemoryPool{T}" /> buffer, which
///     <see cref="MemoryPool{T}.Rent" /> does not zero - so the bytes written after the value were whatever
///     had last been in that pooled memory. Two consequences: a reader stops at the first zero byte among
///     those bytes rather than at the end of the value, so a short string reads back with trailing garbage
///     attached; and the file carries uninitialized process memory, which matters for any file that is
///     shared or archived.
///     <para>
///         Reaching it needs a padding of 256 bytes or more, since below that the writer uses a cleared
///         stackalloc, and it only shows up once something has dirtied the pool - a filtered write does, because
///         compression rents buffers too. Neither a wide string nor a filter alone reproduces it, which is
///         presumably why it went unnoticed.
///     </para>
/// </remarks>
public class FixedLengthStringPaddingTests
{
    /// <summary>Wide enough that a short value's padding lands on the pooled branch.</summary>
    private const int Width = 512;

    /// <summary>
    ///     Fills and returns pooled buffers so that the writer is handed a dirty one, which makes the test
    ///     deterministic instead of dependent on whatever the process left in memory.
    /// </summary>
    private static void DirtyThePool()
    {
        for (int i = 0; i < 8; i++)
        {
            var owner = MemoryPool<byte>.Shared.Rent(Width);
            owner.Memory.Span.Fill(0xAB);
            owner.Dispose();
        }
    }

    private static bool IsAllZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
            if (value != 0)
                return false;

        return true;
    }

    /// <summary>
    ///     A string attribute's width is measured from its own value, so it carries no padding at all - this
    ///     keeps guarding the read side, while the padding itself is asserted through a compound member below.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void AStringAttributeShorterThanTheDeclaredWidthRoundTrips(string value)
    {
        // Arrange
        DirtyThePool();

        var file = new H5File();
        file.Attributes["text"] = value;

        var stream = new MemoryStream();

        // Act
        file.Write(stream, new H5WriteOptions(Width));
        stream.Seek(0, SeekOrigin.Begin);

        using var actual = H5File.Open(stream);

        // Assert
        Assert.Equal(value, actual.Attribute("text").Read<string>());
    }

    /// <summary>
    ///     The case this was found through: a compound member declares the width, and the filter has dirtied
    ///     the pool by the time the padding is written.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("child/")]
    public void ACompoundStringMemberInAFilteredDatasetRoundTrips(string value)
    {
        // Arrange
        DirtyThePool();

        var rows = new Row[64];

        for (int i = 0; i < rows.Length; i++) rows[i] = new Row { Text = value };

        var file = new H5File { ["rows"] = new H5Dataset(rows) };
        var stream = new MemoryStream();

        // Act
        file.Write(stream, new H5WriteOptions(
            Width,
            Filters: [DeflateFilter.Id]));

        stream.Seek(0, SeekOrigin.Begin);

        using var actual = H5File.Open(stream);
        var read = actual.Dataset("rows").Read<Row[]>();

        // Assert
        Assert.All(read, row => Assert.Equal(value, row.Text));
    }

    /// <summary>
    ///     Asserts the bytes in the file, not only the round-trip: a reader that stops at the first zero byte
    ///     hides a leak that happens to begin with one, so the round-trip alone is not sufficient evidence
    ///     that nothing was written.
    /// </summary>
    [Fact]
    public void ThePaddingWrittenToTheFileIsZero()
    {
        // Arrange - a compound member, since that is where a width is declared: an attribute is sized to
        // its own value and leaves no padding to inspect.
        DirtyThePool();

        var file = new H5File
        {
            ["rows"] = new H5Dataset(new Row[] { new() { Text = "xxxxxxxx" } })
        };

        var stream = new MemoryStream();

        // Act - no filter, so the padding is in the file uncompressed and can be inspected.
        file.Write(stream, new H5WriteOptions(Width));

        // Assert
        byte[] bytes = stream.ToArray();
        int valueStart = bytes.AsSpan().IndexOf("xxxxxxxx"u8);

        Assert.True(valueStart >= 0, "the member value was not found in the file");

        Assert.True(
            IsAllZero(bytes.AsSpan(valueStart + 8, Width - 8)),
            "the padding after a fixed-length string contains non-zero bytes, so uninitialized pooled "
            + "memory reached the file");
    }

    private struct Row
    {
        public string Text;
    }
}
using Xunit;

namespace PureHDF.Tests.Reading.VFD;

/// <summary>
/// A <see cref="Stream" /> is allowed to return fewer bytes than asked for, and several real ones do:
/// network streams, pipes and decompressing streams routinely return partial reads. These tests pin
/// the driver's behaviour when that happens.
/// </summary>
public class H5StreamDriverTests
{
    /// <summary>
    /// A stream that never returns more than <paramref name="maxBytesPerRead" /> bytes per call, so a
    /// caller that ignores the returned count - or reads into the wrong slice of its buffer - is caught.
    /// </summary>
    private sealed class ShortReadStream(byte[] data, int maxBytesPerRead) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, Math.Min(count, maxBytesPerRead));

        public override int Read(Span<byte> buffer)
            => _inner.Read(buffer[..Math.Min(buffer.Length, maxBytesPerRead)]);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static byte[] WriteFile()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var data = Enumerable.Range(0, 10_000).ToArray();

            new H5File
            {
                ["group"] = new H5Group
                {
                    ["contiguous"] = new H5Dataset(data)
                }
            }.Write(filePath);

            return File.ReadAllBytes(filePath);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// Dataset payload read through a stream that returns short reads must still be correct.
    /// </summary>
    /// <remarks>
    /// The loop in <c>H5StreamDriver.ReadDataset</c> advanced its <c>remainingBuffer</c> but kept
    /// passing the ORIGINAL buffer to <c>Read</c>, so every partial read landed at offset zero and
    /// overwrote the bytes already there while the loop counted them as progress. The result was
    /// silently wrong data - no exception, no short buffer, just the first few bytes repeated and the
    /// tail left as zeros. A FileStream normally fills the buffer, which is why this went unnoticed.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1024)]
    public void ContiguousDatasetIsCorrectWhenTheStreamReturnsShortReads(int maxBytesPerRead)
    {
        // Arrange
        var expected = Enumerable.Range(0, 10_000).ToArray();

        using var stream = new ShortReadStream(WriteFile(), maxBytesPerRead);
        using var root = H5File.Open(stream, leaveOpen: true);

        // Act
        var actual = root.Group("group").Dataset("contiguous").Read<int[]>();

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Structural reads must be correct too: the driver's small fixed-width reads discarded the count
    /// returned by <c>Stream.Read</c> altogether, so a short read left part of the value as whatever
    /// the stack slot happened to hold. With a one-byte-per-read stream every multi-byte length,
    /// address and checksum in the file decodes to garbage.
    /// </summary>
    [Fact]
    public void FileStructureIsReadableWhenTheStreamReturnsOneByteAtATime()
    {
        // Arrange
        using var stream = new ShortReadStream(WriteFile(), maxBytesPerRead: 1);

        // Act
        using var root = H5File.Open(stream, leaveOpen: true);

        // Assert
        Assert.True(root.LinkExists("group/contiguous"));
        Assert.Equal(10_000UL, root.Group("group").Dataset("contiguous").Space.Dimensions[0]);
    }
}

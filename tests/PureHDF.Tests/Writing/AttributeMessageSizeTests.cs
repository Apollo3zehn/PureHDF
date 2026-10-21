using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
/// An attribute message declares its own size in two bytes, so 65535 is the most it can be - and the data
/// shares that budget with the name, the datatype and the dataspace.
/// </summary>
/// <remarks>
/// A size beyond that used to wrap silently: the object header was allocated from the wrapped number and
/// then written at full length, which left the whole object unreadable rather than just the attribute. The
/// window was narrow - a little over 20 bytes wide, moving with the length of the attribute name - and
/// measuring a string attribute's width made it reachable with ordinary data, since a measured value is
/// stored inline where a variable-length one costs 16 bytes.
/// </remarks>
public class AttributeMessageSizeTests
{
    /// <summary>The largest measured scalar string that still fits, for a single-character name.</summary>
    private const int LargestThatFits = 65_512;

    private static H5File FileWithStringAttribute(string name, int width)
    {
        var file = new H5File();
        file.Attributes[name] = new string('x', width);

        return file;
    }

    private static void AssertRoundTrips(H5File file, string name, Action<IH5Attribute> assert)
    {
        var stream = new MemoryStream();
        file.Write(stream);

        stream.Seek(0, SeekOrigin.Begin);

        using var read = H5File.Open(stream);
        assert(read.Attribute(name));
    }

    [Theory]
    [InlineData(LargestThatFits)]
    [InlineData(LargestThatFits - 1)]
    [InlineData(1024)]
    public void AMeasuredWidthThatFitsStaysFixedLength(int width)
    {
        // Arrange
        var file = FileWithStringAttribute("a", width);

        // Act / Assert
        AssertRoundTrips(file, "a", attribute =>
        {
            Assert.Equal(H5DataTypeClass.String, attribute.Type.Class);
            Assert.Equal(width, attribute.Type.Size);
            Assert.Equal(width, attribute.Read<string[]>()[0].Length);
        });
    }

    /// <summary>
    /// One byte past the limit. Sizing the attribute is the writer's own decision, so it steps back to
    /// variable-length rather than failing a write the caller has no way to see coming.
    /// </summary>
    [Theory]
    [InlineData(LargestThatFits + 1)]
    [InlineData(100_000)]
    [InlineData(4 * 1024 * 1024)]
    public void AMeasuredWidthThatDoesNotFitFallsBackToVariableLength(int width)
    {
        // Arrange
        var file = FileWithStringAttribute("a", width);

        // Act / Assert
        AssertRoundTrips(file, "a", attribute =>
        {
            Assert.Equal(H5DataTypeClass.VariableLength, attribute.Type.Class);
            Assert.Equal(width, attribute.Read<string[]>()[0].Length);
        });
    }

    /// <summary>
    /// The name shares the same two bytes as the data, so it moves where the limit falls. A width that fits
    /// under a short name must still round-trip under a long one.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void TheNameCountsTowardsTheLimit(int nameLength)
    {
        // Arrange
        var name = new string('n', nameLength);
        var file = FileWithStringAttribute(name, LargestThatFits);

        // Act / Assert
        AssertRoundTrips(file, name, attribute =>
            Assert.Equal(LargestThatFits, attribute.Read<string[]>()[0].Length));
    }

    /// <summary>
    /// Many elements reach the limit through the element count rather than a single wide value.
    /// </summary>
    [Fact]
    public void AnArrayTooLargeInTotalFallsBackToVariableLength()
    {
        // Arrange
        var values = Enumerable
            .Range(0, 1_000)
            .Select(_ => new string('x', 100))
            .ToArray();

        var file = new H5File();
        file.Attributes["a"] = values;

        // Act / Assert
        AssertRoundTrips(file, "a", attribute =>
        {
            Assert.Equal(H5DataTypeClass.VariableLength, attribute.Type.Class);
            Assert.Equal(values, attribute.Read<string[]>());
        });
    }

    /// <summary>
    /// The fallback belongs to the measured width only. A width the caller declared is left alone, so it
    /// reports the limit instead of quietly writing a different datatype than was asked for.
    /// </summary>
    [Fact]
    public void ADeclaredWidthThatDoesNotFitThrows()
    {
        // Arrange
        var file = FileWithStringAttribute("a", LargestThatFits + 1);

        var options = new H5WriteOptions(
            DefaultStringLength: LargestThatFits + 1,
            AttributeStringLength: H5AttributeStringLength.Inherit);

        // Act
        void action() => file.Write(new MemoryStream(), options);

        // Assert
        var exception = Assert.Throws<Exception>(action);
        Assert.Contains("object header message can declare", exception.Message);
    }

    /// <summary>
    /// The largest numeric attribute that still fits, pinning the limit from below: the message may use all
    /// 65535 bytes, so a budget counted too conservatively is as wrong as one counted too loosely.
    /// </summary>
    /// <remarks>
    /// 16375 ints is 65500 bytes of data, and the 35 bytes left over are the name, the datatype, the
    /// dataspace and the message's own fixed fields. One element more does not fit.
    /// </remarks>
    [Fact]
    public void TheLargestNumericAttributeThatFitsStillWrites()
    {
        // Arrange
        var expected = Enumerable.Range(0, 16_375).ToArray();

        var file = new H5File();
        file.Attributes["a"] = expected;

        var stream = new MemoryStream();

        // Act
        file.Write(stream);
        stream.Seek(0, SeekOrigin.Begin);

        // Assert
        using var read = H5File.Open(stream);
        Assert.Equal(expected, read.Attribute("a").Read<int[]>());
    }

    /// <summary>
    /// Nothing about the limit is specific to strings, and a non-string attribute has no variable-length
    /// form to fall back to - so it reports the limit rather than writing a file that cannot be read.
    /// </summary>
    /// <remarks>
    /// 16376 is the first count that does not fit; 16384 is 65536 bytes of data exactly, which passed the
    /// old <c>&gt; 64 * 1024</c> comparison and then became 0 as a ushort.
    /// </remarks>
    [Theory]
    [InlineData(16_376)]
    [InlineData(16_378)]
    [InlineData(16_384)]
    [InlineData(16_385)]
    public void ANumericAttributeTooLargeThrows(int count)
    {
        // Arrange
        var file = new H5File();
        file.Attributes["a"] = Enumerable.Range(0, count).ToArray();

        // Act
        void action() => file.Write(new MemoryStream());

        // Assert
        var exception = Assert.Throws<Exception>(action);
        Assert.Contains("object header message can declare", exception.Message);
    }

    /// <summary>
    /// The attribute is not the only thing at stake: the object header is allocated from the message sizes,
    /// so a wrapped one used to take the group's other contents down with it.
    /// </summary>
    [Fact]
    public void TheRestOfTheFileSurvivesAnOversizedAttribute()
    {
        // Arrange
        var file = new H5File
        {
            ["numbers"] = new H5Dataset(Enumerable.Range(0, 256).ToArray())
        };

        file.Attributes["big"] = new string('x', 100_000);
        file.Attributes["small"] = "canary";

        var stream = new MemoryStream();

        // Act
        file.Write(stream);
        stream.Seek(0, SeekOrigin.Begin);

        // Assert
        using var read = H5File.Open(stream);

        Assert.Equal(Enumerable.Range(0, 256), read.Dataset("numbers").Read<int[]>());
        Assert.Equal("canary", read.Attribute("small").Read<string[]>()[0]);
        Assert.Equal(100_000, read.Attribute("big").Read<string[]>()[0].Length);
    }
}

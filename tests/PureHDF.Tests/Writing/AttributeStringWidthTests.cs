using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     A fixed-length string attribute is sized to its own value, not to the file-global
///     <see cref="H5WriteOptions.DefaultStringLength" />.
/// </summary>
/// <remarks>
///     This is a correctness property before it is a size one. A file-global width has to serve the widest
///     attribute anywhere in the file, and anything exceeding it is truncated, so declaring one wide enough to
///     be safe pads every narrower attribute out to it. On a 611 MB report shaped like the files this was
///     measured against, that padding is over half of the 42 MB of metadata.
/// </remarks>
public class AttributeStringWidthTests
{
    /// <summary>Round-trips a file through a memory stream and returns its root group.</summary>
    private static NativeFile RoundTrip(H5File file, H5WriteOptions options)
    {
        var stream = new MemoryStream();
        file.Write(stream, options);
        stream.Seek(0, SeekOrigin.Begin);

        return H5File.Open(stream);
    }

    /// <summary>
    ///     The case where a declared width loses data: a value longer than <c>DefaultStringLength</c>.
    /// </summary>
    [Fact]
    public void AnAttributeLongerThanTheDefaultStringLengthIsNotTruncated()
    {
        // Arrange
        string value = new('x', 500);

        var file = new H5File();
        file.Attributes["text"] = value;

        // Act - a default far narrower than the value, which a measured width has to ignore. Taking the
        // declared width here would clip the value to 8 bytes and report nothing.
        using var actual = RoundTrip(file, new H5WriteOptions(8));

        // Assert
        Assert.Equal(value, actual.Attribute("text").Read<string>());
    }

    /// <summary>
    ///     Truncation mid-character is the worse form of the same defect: it produces bytes that are not
    ///     valid UTF-8 at all, so the loss is not even confined to the tail.
    /// </summary>
    [Fact]
    public void AMultiByteAttributeIsNotCutMidCharacter()
    {
        // "µ" is one character and two UTF-8 bytes, so a width in characters rather than bytes splits it.
        string value = "µµµµ";

        var file = new H5File();
        file.Attributes["unit"] = value;

        using var actual = RoundTrip(file, new H5WriteOptions(5));

        Assert.Equal(value, actual.Attribute("unit").Read<string>());
    }

    /// <summary>
    ///     Each attribute gets its own width, which is what stops one long value from widening every other
    ///     attribute in the file.
    /// </summary>
    [Fact]
    public void EachAttributeIsSizedIndependently()
    {
        var file = new H5File();
        file.Attributes["short"] = "ab";
        file.Attributes["long"] = new string('y', 300);

        using var actual = RoundTrip(file, new H5WriteOptions(300));

        // The declared sizes, not just the values, because the values would round-trip either way.
        Assert.Equal(2, actual.Attribute("short").Type.Size);
        Assert.Equal(300, actual.Attribute("long").Type.Size);

        Assert.Equal("ab", actual.Attribute("short").Read<string>());
    }

    /// <summary>
    ///     An array attribute must be sized by its LONGEST element, or writing it would truncate the others.
    /// </summary>
    [Fact]
    public void AnArrayAttributeIsSizedByItsLongestElement()
    {
        string[] values = new[] { "Wafer", "Reticle", "ChipId", "Device" };

        var file = new H5File();
        file.Attributes["levels"] = values;

        using var actual = RoundTrip(file, new H5WriteOptions(146));

        Assert.Equal("Reticle".Length, actual.Attribute("levels").Type.Size);
        Assert.Equal(values, actual.Attribute("levels").Read<string[]>());
    }

    /// <summary>
    ///     HDF5 has no zero-length string type, so an empty value still needs a byte.
    /// </summary>
    [Fact]
    public void AnEmptyAttributeStillGetsAValidWidth()
    {
        var file = new H5File();
        file.Attributes["empty"] = string.Empty;

        using var actual = RoundTrip(file, new H5WriteOptions(64));

        Assert.Equal(1, actual.Attribute("empty").Type.Size);
        Assert.Equal(string.Empty, actual.Attribute("empty").Read<string>());
    }

    /// <summary>
    ///     A dataset's compound member keeps the width the CALLER declared. Sizing it from one value would
    ///     truncate every longer row and would break the uniform-per-file row type that makes tables in one
    ///     file share a datatype.
    /// </summary>
    [Fact]
    public void ADatasetMemberKeepsTheDeclaredWidth()
    {
        var file = new H5File
        {
            ["table"] = new H5Dataset(new Row[] { new() { Unit = "V" }, new() { Unit = "dBm" } })
        };

        using var actual = RoundTrip(file, new H5WriteOptions(16));

        // 16, not 3: the member is sized by the option, unaffected by what any row happens to hold.
        var member = Assert.Single(
            actual.Dataset("table").Type.Compound.Members,
            member => member.Name == "Unit");

        Assert.Equal(16, member.Type.Size);
    }

    /// <summary>
    ///     A declared width still truncates by default - that is what declaring a width means - so opting
    ///     into failure is how a caller who cannot afford silent loss says so.
    /// </summary>
    [Fact]
    public void ADeclaredWidthTruncatesByDefaultAndThrowsWhenAsked()
    {
        var rows = new Row[] { new() { Unit = "much-longer-than-six" } };

        // Default: the excess is discarded and the write succeeds.
        using (var truncated = RoundTrip(new H5File { ["table"] = new H5Dataset(rows) },
                   new H5WriteOptions(6)))
        {
            Assert.Equal("much-l", truncated.Dataset("table").Read<Row[]>()[0].Unit);
        }

        // Opted in: the write fails instead.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new H5File { ["table"] = new H5Dataset(rows) }.Write(
                new MemoryStream(),
                new H5WriteOptions(6) { StringOverflow = H5StringOverflow.Throw }));

        // The message has to name the byte/character distinction, which is the reason a width chosen by
        // eye is usually too small.
        Assert.Contains("20 UTF-8 bytes", exception.Message);
        Assert.Contains("BYTES, not characters", exception.Message);
    }

    /// <summary>
    ///     A null is refused by a DECLARED width rather than written as an empty value.
    /// </summary>
    /// <remarks>
    ///     A fixed-length field cannot hold the difference between null and empty, so writing one as the other
    ///     discards it silently. An attribute measures its own width and steps back to variable-length when it
    ///     meets a null, so this is only reachable where the caller declared the width - a dataset, a compound
    ///     member, or an attribute set to <see cref="H5AttributeStringLength.Inherit" />.
    /// </remarks>
    [Fact]
    public void ADeclaredWidthRefusesANullValue()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new H5File { ["text"] = new[] { "A", null } }.Write(
                new MemoryStream(),
                new H5WriteOptions(6)));

        // The message has to say what to do about it, since a null is not something StringOverflow covers.
        Assert.Contains("null string", exception.Message);
        Assert.Contains("variable-length", exception.Message);
    }

    /// <summary>
    ///     Opting in must not make a value that FITS throw, including one that fills its width exactly.
    /// </summary>
    [Fact]
    public void AValueThatExactlyFillsItsWidthDoesNotThrow()
    {
        var rows = new Row[] { new() { Unit = "abcdef" } };

        using var actual = RoundTrip(
            new H5File { ["table"] = new H5Dataset(rows) },
            new H5WriteOptions(6) { StringOverflow = H5StringOverflow.Throw });

        Assert.Equal("abcdef", actual.Dataset("table").Read<Row[]>()[0].Unit);
    }

    /// <summary>
    ///     A measured width declares <c>H5T_STR_NULLPAD</c>, and a declared width keeps
    ///     <c>H5T_STR_NULLTERM</c> - even when the two widths coincide.
    /// </summary>
    /// <remarks>
    ///     The padding is not cosmetic. A measured width is filled to the last byte, so NULLTERM would promise
    ///     a terminator that is not in the file; and the C library reserves the final byte of a NULLTERM field
    ///     when it converts a value into it, so a tool rewriting a measured attribute through a wider datatype
    ///     would lose the last character. NULLPAD leaves the whole width writable.
    ///     <para>
    ///         Both cases come from ONE file on purpose. Datatype messages are cached per write, keyed by
    ///         everything the message depends on, so an attribute measured at six bytes and a compound member
    ///         declared at six bytes must not be handed the same message.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AMeasuredWidthDeclaresNullPadAndADeclaredWidthDoesNot()
    {
        // Arrange - the attribute measures to exactly the width the compound member declares.
        var file = new H5File
        {
            ["table"] = new H5Dataset(new Row[] { new() { Unit = "abcdef" } })
        };

        file.Attributes["measured"] = "abcdef";

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(6));

        var attributeType = ((NativeAttribute)actual.Attribute("measured")).InternalElementDataType;
        var compoundType = ((NativeDataset)actual.Dataset("table")).InternalDataType;

        var memberType = ((CompoundPropertyDescription)compoundType.Properties
                .Single(property => ((CompoundPropertyDescription)property).Name == "Unit"))
            .MemberTypeMessage;

        // Assert
        Assert.Equal(6u, attributeType.Size);
        Assert.Equal(PaddingType.NullPad, ((StringBitFieldDescription)attributeType.BitField).PaddingType);

        Assert.Equal(6u, memberType.Size);
        Assert.Equal(PaddingType.NullTerminate, ((StringBitFieldDescription)memberType.BitField).PaddingType);

        // ... and both still read back, which is what the padding is ultimately about.
        Assert.Equal("abcdef", actual.Attribute("measured").Read<string>());
        Assert.Equal("abcdef", actual.Dataset("table").Read<Row[]>()[0].Unit);
    }

    /// <summary>
    ///     A measured width declares <c>H5T_STR_NULLPAD</c> whether or not the values happen to share a
    ///     length, because the LONGEST one always fills the field exactly.
    /// </summary>
    /// <remarks>
    ///     The distinction that matters is not how the field is padded but whether its last byte is spoken
    ///     for. NULLTERM reserves it, and the C library honours that when converting a value in, so a tool
    ///     rewriting this attribute through a wider datatype would drop the last character of "Reticle" -
    ///     the very element the width was measured from. Reads are unaffected either way: there is no
    ///     fixed-to-variable conversion path in the C library, so a consumer reads the field into a
    ///     fixed-width buffer and finds the same bytes under both declarations.
    /// </remarks>
    [Fact]
    public void AMeasuredWidthDeclaresNullPadWhateverTheValueLengths()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["levels"] = new[] { "Wafer", "Reticle" };
        file.Attributes["uniform"] = new[] { "abc", "xyz" };

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(32));

        var mixed = ((NativeAttribute)actual.Attribute("levels")).InternalElementDataType;
        var uniform = ((NativeAttribute)actual.Attribute("uniform")).InternalElementDataType;

        // Assert - sized to the longest, and NullPad in both cases.
        Assert.Equal(7u, mixed.Size);
        Assert.Equal(PaddingType.NullPad, ((StringBitFieldDescription)mixed.BitField).PaddingType);

        Assert.Equal(3u, uniform.Size);
        Assert.Equal(PaddingType.NullPad, ((StringBitFieldDescription)uniform.BitField).PaddingType);

        // The shorter value still reads back without its padding.
        Assert.Equal<string[]>(["Wafer", "Reticle"], actual.Attribute("levels").Read<string[]>());
        Assert.Equal<string[]>(["abc", "xyz"], actual.Attribute("uniform").Read<string[]>());
    }

    private struct Row
    {
        public string Unit;
    }
}
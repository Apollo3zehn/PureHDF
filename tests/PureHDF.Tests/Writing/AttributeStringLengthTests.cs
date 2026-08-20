using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     <see cref="H5WriteOptions.AttributeStringLength" /> decides how a string attribute is sized, on its own
///     rather than as a modifier on <see cref="H5WriteOptions.DefaultStringLength" />.
/// </summary>
/// <remarks>
///     The two settings have to be able to disagree. A file can want variable-length datasets and measured
///     attributes, or a declared width for its compound members and variable-length attributes - neither is
///     expressible while one option decides both.
/// </remarks>
public class AttributeStringLengthTests
{
    /// <summary>Round-trips a file through a memory stream and returns its root group.</summary>
    private static NativeFile RoundTrip(H5File file, H5WriteOptions options)
    {
        var stream = new MemoryStream();
        file.Write(stream, options);
        stream.Seek(0, SeekOrigin.Begin);

        return H5File.Open(stream);
    }

    private static DatatypeMessage ElementTypeOf(NativeFile file, string name)
    {
        return ((NativeAttribute)file.Attribute(name)).InternalElementDataType;
    }

    /// <summary>
    ///     Measuring does not wait for <see cref="H5WriteOptions.DefaultStringLength" /> to be set. Wanting
    ///     variable-length datasets says nothing about wanting variable-length attributes.
    /// </summary>
    [Fact]
    public void AnAttributeIsMeasuredWithoutADefaultStringLength()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["unit"] = "5 µm";

        // Act - default options throughout, so datasets would still be variable-length here.
        using var actual = RoundTrip(file, new H5WriteOptions());

        // Assert - 5 bytes for 4 characters, because µ takes two.
        var datatype = ElementTypeOf(actual, "unit");

        Assert.Equal(DatatypeMessageClass.String, datatype.Class);
        Assert.Equal(5u, datatype.Size);
        Assert.Equal("5 µm", actual.Attribute("unit").Read<string>());
    }

    /// <summary>
    ///     <see cref="H5AttributeStringLength.Inherit" /> hands attributes back to
    ///     <see cref="H5WriteOptions.DefaultStringLength" />, truncation included.
    /// </summary>
    [Fact]
    public void InheritTakesTheDeclaredWidth()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["unit"] = "abcdefgh";

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(6)
        {
            AttributeStringLength = H5AttributeStringLength.Inherit
        });

        // Assert
        var datatype = ElementTypeOf(actual, "unit");

        Assert.Equal(DatatypeMessageClass.String, datatype.Class);
        Assert.Equal(6u, datatype.Size);
        Assert.Equal(PaddingType.NullTerminate, ((StringBitFieldDescription)datatype.BitField).PaddingType);

        // The declared width truncates, which is exactly what Inherit is asking to keep.
        Assert.Equal("abcdef", actual.Attribute("unit").Read<string>());
    }

    /// <summary>
    ///     A zero <see cref="H5WriteOptions.DefaultStringLength" /> asks for variable-length strings, so
    ///     inheriting it gives an attribute the same.
    /// </summary>
    [Fact]
    public void InheritWithoutADefaultStringLengthIsVariableLength()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["unit"] = "5 µm";

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions
        {
            AttributeStringLength = H5AttributeStringLength.Inherit
        });

        // Assert
        Assert.Equal(DatatypeMessageClass.VariableLength, ElementTypeOf(actual, "unit").Class);
        Assert.Equal("5 µm", actual.Attribute("unit").Read<string>());
    }

    /// <summary>
    ///     The other direction: a declared width for the compound members in the file, without it reaching the
    ///     attributes.
    /// </summary>
    [Fact]
    public void VariableLengthIgnoresADeclaredWidth()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["unit"] = "5 µm";

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(32)
        {
            AttributeStringLength = H5AttributeStringLength.VariableLength
        });

        // Assert
        Assert.Equal(DatatypeMessageClass.VariableLength, ElementTypeOf(actual, "unit").Class);
        Assert.Equal("5 µm", actual.Attribute("unit").Read<string>());
    }

    /// <summary>
    ///     The setting reaches an attribute whose elements are strings, not a string that is a MEMBER of one.
    /// </summary>
    /// <remarks>
    ///     A member's width has to stay uniform across every object sharing the type, so it keeps coming from
    ///     <see cref="H5WriteOptions.DefaultStringLength" /> and the mappers - the same reason datasets are left
    ///     alone.
    /// </remarks>
    [Theory]
    [InlineData(H5AttributeStringLength.Measured)]
    [InlineData(H5AttributeStringLength.Inherit)]
    [InlineData(H5AttributeStringLength.VariableLength)]
    public void ACompoundMemberIgnoresTheMode(H5AttributeStringLength mode)
    {
        // Arrange
        var file = new H5File();
        file.Attributes["row"] = new Row { Unit = "abcdefgh" };

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(6)
        {
            AttributeStringLength = mode
        });

        // Assert
        var compound = ElementTypeOf(actual, "row");

        var memberType = ((CompoundPropertyDescription)compound.Properties
                .Single(property => ((CompoundPropertyDescription)property).Name == "Unit"))
            .MemberTypeMessage;

        Assert.Equal(DatatypeMessageClass.String, memberType.Class);
        Assert.Equal(6u, memberType.Size);
    }

    /// <summary>
    ///     A null element takes the attribute back to variable-length, because that is the only kind of string
    ///     field that can hold a null at all.
    /// </summary>
    /// <remarks>
    ///     The case that keeps measuring honest. A measured fixed-length field would have to write the null as
    ///     an all-padding value and read it back as "", which is the same silent loss that measuring exists to
    ///     avoid for over-long values.
    /// </remarks>
    [Fact]
    public void AnAttributeWithANullElementStaysVariableLength()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["levels"] = new[] { "A", null, "CBA" };
        file.Attributes["complete"] = new[] { "A", "CBA" };

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions());

        // Assert
        Assert.Equal(DatatypeMessageClass.VariableLength, ElementTypeOf(actual, "levels").Class);
        Assert.Equal<string?[]>(["A", null, "CBA"], actual.Attribute("levels").Read<string?[]>());

        // The same shape of data without the null is still measured.
        Assert.Equal(DatatypeMessageClass.String, ElementTypeOf(actual, "complete").Class);
        Assert.Equal(3u, ElementTypeOf(actual, "complete").Size);
    }

    private class Row
    {
        public string Unit { get; set; } = default!;
    }
}
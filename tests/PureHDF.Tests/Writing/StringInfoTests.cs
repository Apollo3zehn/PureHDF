using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace PureHDF.Tests.Writing;

/// <summary>
///     <see cref="H5StringInfo" /> sizes the strings of one dataset or attribute, where
///     <see cref="H5WriteOptions" /> can only size them for the whole file.
/// </summary>
public class StringInfoTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NameValueUnit
    {
        public string Name;
        public double Value;
        public string Unit;
    }

    private static NativeFile RoundTrip(H5File file, H5WriteOptions? options = default)
    {
        var stream = new MemoryStream();
        file.Write(stream, options ?? new H5WriteOptions());
        stream.Seek(0, SeekOrigin.Begin);

        return H5File.Open(stream);
    }

    private static DatatypeMessage DatasetType(NativeFile file, string name)
    {
        return ((NativeDataset)file.Dataset(name)).InternalDataType;
    }

    private static DatatypeMessage MemberType(DatatypeMessage compound, string member)
    {
        return compound.Properties
            .Cast<CompoundPropertyDescription>()
            .Single(property => property.Name == member)
            .MemberTypeMessage;
    }

    private static PaddingType PaddingOf(DatatypeMessage datatype)
    {
        return ((StringBitFieldDescription)datatype.BitField).PaddingType;
    }

    private static Func<MemberInfo, int?> Widths(int name, int unit)
    {
        return member => member.Name switch
        {
            nameof(NameValueUnit.Name) => name,
            nameof(NameValueUnit.Unit) => unit,
            _ => null
        };
    }

    [Fact]
    public void EachStringDatasetTakesItsOwnLength()
    {
        // Arrange
        var file = new H5File
        {
            ["short"] = new H5Dataset(new[] { "ab", "cd" }, stringInfo: new H5StringInfo(Length: 4)),
            ["long"] = new H5Dataset(new[] { "abcdefgh" }, stringInfo: new H5StringInfo(Length: 9)),
            ["default"] = new H5Dataset(new[] { "abcdefgh" }),
        };

        // Act
        using var actual = RoundTrip(file);

        // Assert
        Assert.Equal(DatatypeMessageClass.String, DatasetType(actual, "short").Class);
        Assert.Equal(4u, DatasetType(actual, "short").Size);
        Assert.Equal(9u, DatasetType(actual, "long").Size);
        Assert.Equal(DatatypeMessageClass.VariableLength, DatasetType(actual, "default").Class);

        Assert.Equal(["ab", "cd"], actual.Dataset("short").Read<string[]>());
        Assert.Equal(["abcdefgh"], actual.Dataset("long").Read<string[]>());
    }

    [Fact]
    public void ALengthOfZeroIsVariableLengthWhateverTheDefaultStringLength()
    {
        // Arrange
        var file = new H5File
        {
            ["text"] = new H5Dataset(new[] { "a value longer than the default" }, stringInfo: new H5StringInfo(Length: 0))
        };

        // Act
        using var actual = RoundTrip(file, new H5WriteOptions(DefaultStringLength: 4));

        // Assert
        Assert.Equal(DatatypeMessageClass.VariableLength, DatasetType(actual, "text").Class);
        Assert.Equal(["a value longer than the default"], actual.Dataset("text").Read<string[]>());
    }

    [Fact]
    public void DatasetsOfOneRowTypeTakeTheirOwnMemberLengths()
    {
        // Arrange
        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };

        var file = new H5File
        {
            ["narrow"] = new H5Dataset(rows, stringInfo: new H5StringInfo(MemberLength: Widths(name: 4, unit: 2))),
            ["wide"] = new H5Dataset(rows, stringInfo: new H5StringInfo(MemberLength: Widths(name: 16, unit: 8))),
        };

        // Act
        using var actual = RoundTrip(file);

        // Assert
        Assert.Equal(4u, MemberType(DatasetType(actual, "narrow"), "Name").Size);
        Assert.Equal(2u, MemberType(DatasetType(actual, "narrow"), "Unit").Size);
        Assert.Equal(16u, MemberType(DatasetType(actual, "wide"), "Name").Size);
        Assert.Equal(8u, MemberType(DatasetType(actual, "wide"), "Unit").Size);

        foreach (var name in new[] { "narrow", "wide" })
        {
            var row = actual.Dataset(name).Read<NameValueUnit[]>().Single();

            Assert.Equal("gain", row.Name);
            Assert.Equal(1.5, row.Value);
            Assert.Equal("dB", row.Unit);
        }
    }

    [Fact]
    public void MemberLengthComesBeforeTheMapperAndLengthAfterIt()
    {
        // Arrange
        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };

        var stringInfo = new H5StringInfo(
            Length: 5,
            MemberLength: member => member.Name == nameof(NameValueUnit.Name) ? 6 : null);

        var file = new H5File
        {
            ["rows"] = new H5Dataset(rows, stringInfo: stringInfo),
            ["other"] = new H5Dataset(rows),
        };

        // Act - the mapper only answers for Unit.
        using var actual = RoundTrip(file, new H5WriteOptions(
            DefaultStringLength: 3,
            FieldStringLengthMapper: field => field.Name == nameof(NameValueUnit.Unit) ? 7 : null));

        // Assert
        Assert.Equal(6u, MemberType(DatasetType(actual, "rows"), "Name").Size);
        Assert.Equal(7u, MemberType(DatasetType(actual, "rows"), "Unit").Size);

        // Without a string info, the file-wide options size everything.
        Assert.Equal(3u, MemberType(DatasetType(actual, "other"), "Name").Size);
        Assert.Equal(7u, MemberType(DatasetType(actual, "other"), "Unit").Size);
    }

    [Fact]
    public void LengthSizesTheMembersNothingElseAnswersFor()
    {
        // Arrange
        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };

        var file = new H5File
        {
            ["rows"] = new H5Dataset(rows, stringInfo: new H5StringInfo(Length: 5))
        };

        // Act
        using var actual = RoundTrip(file);

        // Assert
        Assert.Equal(5u, MemberType(DatasetType(actual, "rows"), "Name").Size);
        Assert.Equal(5u, MemberType(DatasetType(actual, "rows"), "Unit").Size);
    }

    [Fact]
    public void DatasetsAskingForTheSameLengthsShareOneDatatype()
    {
        // Arrange - a new delegate per dataset, as a caller building the info per table would have.
        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };
        var file = new H5File();

        for (var i = 0; i < 50; i++)
        {
            file[$"rows{i}"] = new H5Dataset(rows, stringInfo: new H5StringInfo(MemberLength: Widths(name: 4 + i % 2, unit: 2)));
        }

        var stream = new MemoryStream();

        // Act
        using var writer = file.BeginWrite(stream);

        // Assert - one compound per distinct set of lengths, not one per dataset.
        var compounds = writer.Context.TypeToMessageMap.Keys
            .Count(key => key.Type == typeof(NameValueUnit));

        Assert.Equal(2, compounds);
    }

    [Theory]
    [InlineData(H5StringPadding.NullTerminate)]
    [InlineData(H5StringPadding.NullPad)]
    [InlineData(H5StringPadding.SpacePad)]
    public void PaddingIsDeclaredForTheDatasetAndItsMembers(H5StringPadding padding)
    {
        // Arrange
        var expected = padding switch
        {
            H5StringPadding.NullTerminate => PaddingType.NullTerminate,
            H5StringPadding.NullPad => PaddingType.NullPad,
            _ => PaddingType.SpacePad
        };

        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };
        var stringInfo = new H5StringInfo(Length: 8, Padding: padding);

        var file = new H5File
        {
            ["text"] = new H5Dataset(new[] { "ab" }, stringInfo: stringInfo),
            ["rows"] = new H5Dataset(rows, stringInfo: stringInfo),
        };

        // Act
        using var actual = RoundTrip(file);

        // Assert
        Assert.Equal(expected, PaddingOf(DatasetType(actual, "text")));
        Assert.Equal(expected, PaddingOf(MemberType(DatasetType(actual, "rows"), "Name")));

        Assert.Equal(["ab"], actual.Dataset("text").Read<string[]>());
        Assert.Equal("dB", actual.Dataset("rows").Read<NameValueUnit[]>().Single().Unit);
    }

    [Fact]
    public void SpacePadFillsTheFieldWithSpaces()
    {
        // Arrange
        var file = new H5File
        {
            ["text"] = new H5Dataset(new[] { "xyzzy" }, stringInfo: new H5StringInfo(Length: 8, Padding: H5StringPadding.SpacePad))
        };

        var stream = new MemoryStream();

        // Act
        file.Write(stream);

        // Assert
        var bytes = stream.ToArray();
        var start = bytes.AsSpan().IndexOf("xyzzy"u8);

        Assert.True(start >= 0, "the value was not found in the file");
        Assert.Equal("xyzzy   "u8.ToArray(), bytes.AsSpan(start, 8).ToArray());
    }

    [Fact]
    public void TheHdf5LibraryReadsTheDeclaredPadding()
    {
        // Arrange
        var filePath = Path.GetTempFileName();

        var file = new H5File
        {
            ["text"] = new H5Dataset(new[] { "ab" }, stringInfo: new H5StringInfo(Length: 4, Padding: H5StringPadding.SpacePad))
        };

        try
        {
            // Act
            file.Write(filePath);
            var dump = TestUtils.DumpH5File(filePath);

            // Assert
            Assert.Contains("STRSIZE 4;", dump);
            Assert.Contains("STRPAD H5T_STR_SPACEPAD;", dump);
            Assert.Contains("\"ab  \"", dump);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ADeferredDatasetKeepsItsLengths()
    {
        // Arrange
        var dataset = new H5Dataset<NameValueUnit[]>(
            fileDims: [1],
            stringInfo: new H5StringInfo(MemberLength: Widths(name: 4, unit: 2)));

        var file = new H5File { ["rows"] = dataset };
        var stream = new MemoryStream();

        // Act
        using (var writer = file.BeginWrite(stream))
        {
            writer.Write(dataset, [new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" }]);
        }

        stream.Seek(0, SeekOrigin.Begin);
        using var actual = H5File.Open(stream);

        // Assert
        Assert.Equal(4u, MemberType(DatasetType(actual, "rows"), "Name").Size);
        Assert.Equal(2u, MemberType(DatasetType(actual, "rows"), "Unit").Size);
        Assert.Equal("gain", actual.Dataset("rows").Read<NameValueUnit[]>().Single().Name);
    }

    [Fact]
    public void AValueLongerThanTheLengthFollowsStringOverflow()
    {
        // Arrange
        H5File CreateFile() => new()
        {
            ["text"] = new H5Dataset(new[] { "abcdef" }, stringInfo: new H5StringInfo(Length: 3))
        };

        // Act
        using var truncated = RoundTrip(CreateFile());
        void action() => RoundTrip(CreateFile(), new H5WriteOptions(StringOverflow: H5StringOverflow.Throw));

        // Assert
        Assert.Equal(["abc"], truncated.Dataset("text").Read<string[]>());
        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void ANegativeLengthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new H5StringInfo(Length: -1));
    }

    [Fact]
    public void ANegativeMemberLengthIsRejected()
    {
        // Arrange
        var rows = new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } };

        var file = new H5File
        {
            ["rows"] = new H5Dataset(rows, stringInfo: new H5StringInfo(MemberLength: _ => -1))
        };

        // Act
        void action() => RoundTrip(file);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains(nameof(NameValueUnit.Name), exception.Message);
    }

    [Fact]
    public void AnAttributeLengthOverridesAttributeStringLength()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["measured"] = new H5Attribute("abc", stringInfo: new H5StringInfo(Length: 10));
        file.Attributes["variable"] = new H5Attribute("abc", stringInfo: new H5StringInfo(Length: 0));

        // Act - Inherit with a declared width would size both to 4 without the string info.
        using var actual = RoundTrip(file, new H5WriteOptions(DefaultStringLength: 4)
        {
            AttributeStringLength = H5AttributeStringLength.Inherit
        });

        // Assert
        var measured = ((NativeAttribute)actual.Attribute("measured")).InternalElementDataType;
        var variable = ((NativeAttribute)actual.Attribute("variable")).InternalElementDataType;

        Assert.Equal(10u, measured.Size);
        Assert.Equal(DatatypeMessageClass.VariableLength, variable.Class);
        Assert.Equal("abc", actual.Attribute("measured").Read<string>());
        Assert.Equal("abc", actual.Attribute("variable").Read<string>());
    }

    [Fact]
    public void ACompoundAttributeTakesItsMemberLengths()
    {
        // Arrange
        var file = new H5File();

        file.Attributes["row"] = new H5Attribute(
            new[] { new NameValueUnit { Name = "gain", Value = 1.5, Unit = "dB" } },
            stringInfo: new H5StringInfo(MemberLength: Widths(name: 4, unit: 2)));

        // Act
        using var actual = RoundTrip(file);

        // Assert
        var datatype = ((NativeAttribute)actual.Attribute("row")).InternalElementDataType;

        Assert.Equal(4u, MemberType(datatype, "Name").Size);
        Assert.Equal(2u, MemberType(datatype, "Unit").Size);
    }
}

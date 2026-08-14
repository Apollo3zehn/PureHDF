using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace PureHDF.Tests.RoundTrip;

public class FixedLengthStringRoundTripTests
{
    // ASCII, a 2-byte character, a 3-byte character and a 4-byte character.
    private const string NonAscii = "5µm→x\U0001F600";

    private static int Utf8Length => Encoding.UTF8.GetByteCount(NonAscii);

    [Fact]
    public void WriteAndReadFixedLengthStringDataset()
    {
        var actual = RoundTrip(
            file => file["test"] = new string[] { NonAscii },
            file => file.Dataset("test").Read<string[]>()[0]);

        Assert.Equal(NonAscii, actual);
    }

    [Fact]
    public void WriteAndReadFixedLengthStringCompoundMember()
    {
        var actual = RoundTrip(
            file => file["test"] = new Row[] { new() { Text = NonAscii } },
            file => file.Dataset("test").Read<Row[]>()[0].Text);

        Assert.Equal(NonAscii, actual);
    }

    [Fact]
    public void WriteAndReadFixedLengthStringAttribute()
    {
        var actual = RoundTrip(
            file => file.Attributes["test"] = NonAscii,
            file => file.Attribute("test").Read<string>());

        Assert.Equal(NonAscii, actual);
    }

    /// <summary>
    /// Guards the variable-length path, which already decoded UTF-8 and must keep doing so.
    /// </summary>
    [Fact]
    public void WriteAndReadVariableLengthStringDataset()
    {
        var h5FileWrite = new H5File
        {
            ["test"] = new string[] { NonAscii }
        };

        var memoryStream = new MemoryStream();
        h5FileWrite.Write(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var actual = H5File.Open(memoryStream).Dataset("test").Read<string[]>()[0];

        Assert.Equal(NonAscii, actual);
    }

    private static string RoundTrip(Action<H5File> arrange, Func<IH5Group, string> act)
    {
        // Arrange
        var h5FileWrite = new H5File();
        arrange(h5FileWrite);

        var memoryStream = new MemoryStream();

        // A non-zero DefaultStringLength is what makes every string in the file
        // fixed-length rather than variable-length.
        h5FileWrite.Write(memoryStream, new H5WriteOptions(
            DefaultStringLength: Utf8Length,
            FieldStringLengthMapper: _ => Utf8Length));

        memoryStream.Seek(0, SeekOrigin.Begin);

        // Act
        return act(H5File.Open(memoryStream));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Row
    {
        public string Text;
    }
}

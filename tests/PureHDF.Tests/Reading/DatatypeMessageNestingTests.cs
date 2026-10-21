using PureHDF.VFD;
using Xunit;

namespace PureHDF.Tests.Reading;

/// <summary>
/// A datatype message contains its base type inline, so decoding one is recursive - and the nesting depth
/// comes from the file being read.
/// </summary>
/// <remarks>
/// Without a bound, a file that nests deeply enough exhausts the stack. That is not a catchable failure:
/// the runtime terminates the process on <c>StackOverflowException</c>, so a caller cannot defend itself
/// against a malformed or hostile file the way it can against every other decoding error. The bound is
/// <see cref="H5ReadOptions.MaxDatatypeNestingDepth" />, so a file that genuinely nests further can still
/// be read.
/// </remarks>
public class DatatypeMessageNestingTests
{
    /// <summary>
    /// A variable-length datatype message, whose base type is whatever follows it. Eight bytes per level:
    /// the class/version byte, three bytes of class bit field, and the four-byte size.
    /// </summary>
    private static void WriteVariableLengthLevel(BinaryWriter writer)
    {
        writer.Write((byte)(1 << 4 | (byte)DatatypeMessageClass.VariableLength));
        writer.Write((byte)(byte)InternalVariableLengthType.Sequence);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((uint)16);
    }

    /// <summary>A one-byte unsigned fixed-point type, to terminate the nesting.</summary>
    private static void WriteFixedPointTerminator(BinaryWriter writer)
    {
        writer.Write((byte)(1 << 4 | (byte)DatatypeMessageClass.FixedPoint));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((uint)1);
        writer.Write((ushort)0);
        writer.Write((ushort)8);
    }

    private static H5DriverBase DriverForNesting(int levels)
    {
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);

        for (int i = 0; i < levels; i++)
            WriteVariableLengthLevel(writer);

        WriteFixedPointTerminator(writer);

        writer.Flush();
        stream.Seek(0, SeekOrigin.Begin);

        return new H5StreamDriver(stream, leaveOpen: false);
    }

    /// <summary>
    /// Nesting a real file could plausibly use keeps working. A compound of arrays of compounds is a few
    /// levels deep; nothing legitimate approaches the bound.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(32)]
    public async Task ADatatypeNestedToAReasonableDepthDecodes(int levels)
    {
        // Arrange
        using var driver = DriverForNesting(levels);

        // Act
        var message = await DatatypeMessage
            .Decode(driver, new H5ReadOptions().MaxDatatypeNestingDepth);

        // Assert
        var depth = 0;

        while (message.Class == DatatypeMessageClass.VariableLength)
        {
            depth++;
            message = ((VariableLengthPropertyDescription)message.Properties[0]).BaseType;
        }

        Assert.Equal(levels, depth);
        Assert.Equal(DatatypeMessageClass.FixedPoint, message.Class);
    }

    /// <summary>
    /// Past the bound it reports the file as unsupported, which a caller can catch - rather than recursing
    /// until the stack runs out, which no caller can.
    /// </summary>
    [Theory]
    [InlineData(1_000)]
    [InlineData(100_000)]
    public async Task ADatatypeNestedTooDeeplyThrowsInsteadOfExhaustingTheStack(int levels)
    {
        // Arrange
        using var driver = DriverForNesting(levels);

        // Act
        async Task action() => await DatatypeMessage
            .Decode(driver, new H5ReadOptions().MaxDatatypeNestingDepth);

        // Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(action);
        Assert.Contains(nameof(H5ReadOptions.MaxDatatypeNestingDepth), exception.Message);
    }

    /// <summary>
    /// The bound is the caller's to set, so a file that genuinely nests further can still be read - which
    /// is what keeps the default from being a limit on what PureHDF can open.
    /// </summary>
    [Fact]
    public async Task TheBoundCanBeRaised()
    {
        // Arrange
        var levels = new H5ReadOptions().MaxDatatypeNestingDepth + 10;

        using var refused = DriverForNesting(levels);
        using var allowed = DriverForNesting(levels);

        // Act
        async Task tooDeep() => await DatatypeMessage
            .Decode(refused, new H5ReadOptions().MaxDatatypeNestingDepth);

        var message = await DatatypeMessage
            .Decode(allowed, new H5ReadOptions(MaxDatatypeNestingDepth: levels).MaxDatatypeNestingDepth);

        // Assert
        await Assert.ThrowsAsync<NotSupportedException>(tooDeep);
        Assert.Equal(DatatypeMessageClass.VariableLength, message.Class);
    }

    /// <summary>
    /// Lowering it works too, so a caller reading untrusted files can be stricter than the default.
    /// </summary>
    [Fact]
    public async Task TheBoundCanBeLowered()
    {
        // Arrange
        using var driver = DriverForNesting(8);

        // Act
        async Task action() => await DatatypeMessage
            .Decode(driver, new H5ReadOptions(MaxDatatypeNestingDepth: 4).MaxDatatypeNestingDepth);

        // Assert
        await Assert.ThrowsAsync<NotSupportedException>(action);
    }
}

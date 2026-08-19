using HDF.PInvoke;
using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Reading.Async;

/// <summary>
/// Covers <see cref="IH5Attribute.ReadAsync{T}(ulong[], System.Threading.CancellationToken)" />.
/// </summary>
/// <remarks>
/// The distinction these tests are built around: an attribute's own bytes are decoded into the object
/// header when the object is resolved, so a FIXED-SIZE attribute is already in memory by the time it can
/// be read and nothing suspends. A VARIABLE-LENGTH or reference datatype stores only a global heap ID
/// inline, and resolving one seeks and reads the file - so those are the attributes that genuinely need
/// an async read, and the ones a host that cannot block depends on it for.
/// <para>
/// Both cases are covered, and the fixed-size one asserts that it costs no reads at all rather than
/// merely that it returns the right answer - otherwise a change that started routing fixed-size
/// attribute reads through the file would pass unnoticed.
/// </para>
/// <para>
/// WHAT THESE TESTS DO NOT PROVE: that the async path never blocks. Blocking is only fatal where there
/// is no thread to complete the read on - a single-threaded WASM runtime - and that cannot be reproduced
/// here, because the library awaits with <c>ConfigureAwait(false)</c> and a test host always has a
/// thread pool to run those continuations on. A bridge left behind on one of these paths would
/// therefore still pass.
/// </para>
/// </remarks>
[Collection(SharedHdf5StateCollection.Name)]
public class AsyncAttributeReadTests
{
    // The values TestUtils.AddString writes to the "variable" attribute, in order.
    private static readonly string[] _expectedStrings =
        ["001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!"];

    private readonly ITestOutputHelper _output;

    public AsyncAttributeReadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// The case that matters: a variable-length attribute resolves global heap IDs, so it reads the file.
    /// </summary>
    [Fact]
    public async Task ReadAsyncReadsVariableLengthData()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        // Act
        var actual = await attribute.ReadAsync<string[]>();

        // Assert - the values, not just parity with Read, so that both paths cannot be wrong together.
        Assert.Equal(_expectedStrings, actual);
    }

    /// <summary>
    /// The same read with every underlying read suspended and resumed on the thread pool.
    /// </summary>
    /// <remarks>
    /// This is the shape of the case the async surface exists for. It is also the one that would catch a
    /// decoder that captured state across an await incorrectly, since every continuation here resumes on
    /// a different thread than the one that issued the read.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncOfVariableLengthDataWorksWhenEveryReadSuspends()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: true);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        // Act
        var actual = await attribute.ReadAsync<string[]>();

        // Assert
        Assert.Equal(_expectedStrings, actual);
    }

    /// <summary>
    /// A fixed-size attribute must be served entirely from the object header, without reading the file.
    /// </summary>
    /// <remarks>
    /// Asserting zero reads rather than just correctness: this is what makes the claim in the interface
    /// documentation - that a fixed-size attribute completes synchronously because its bytes are already
    /// held - a tested statement rather than a comment.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncOfFixedSizeDataCostsNoReads()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("fixed+nullterm");

        // Resolve the attribute and warm anything its object header needs.
        _ = attribute.Read<string[]>();

        // Act
        stream.ResetCounts();
        var actual = await attribute.ReadAsync<string[]>();

        _output.WriteLine($"{stream.MetadataReadCount} metadata reads, {stream.DatasetReadCount} dataset reads");

        // Assert
        Assert.Equal(0, stream.MetadataReadCount);
        Assert.Equal(0, stream.DatasetReadCount);
        Assert.Equal(12, actual.Length);
        Assert.Equal("00", actual[0]);
    }

    /// <summary>
    /// The other half of the same claim: a variable-length attribute DOES read the file, which is why it
    /// needs an async read in the first place.
    /// </summary>
    /// <remarks>
    /// Measured on a fresh stream, because the global heap collection is cached after the first resolve -
    /// a second read of the same attribute costs nothing and would make this pass for the wrong reason.
    /// Asserting only that it is non-zero: the exact count is a property of the heap layout, not of this
    /// behavior, and pinning it would make the test brittle for no gain.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncOfVariableLengthDataDoesReadTheFile()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        // Resolve the attribute, so that what follows is the value read and not the navigation to it.
        _ = attribute.Name;

        // Act
        stream.ResetCounts();
        var actual = await attribute.ReadAsync<string[]>();

        _output.WriteLine($"variable-length first read: {stream.MetadataReadCount} metadata reads");

        // Assert
        Assert.True(
            stream.MetadataReadCount > 0,
            "A variable-length attribute resolves global heap IDs, so the first read must touch the file.");

        Assert.Equal(_expectedStrings, actual);
    }

    /// <summary>
    /// The asynchronous and synchronous surfaces must agree, across both datatype classes.
    /// </summary>
    [Theory]
    [InlineData("variable")]
    [InlineData("fixed+nullterm")]
    [InlineData("fixed+nullpad")]
    [InlineData("fixed+spacepad")]
    public async Task ReadAsyncAgreesWithRead(string attributeName)
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute(attributeName);

        // Act
        var expected = attribute.Read<string[]>();
        var actual = await attribute.ReadAsync<string[]>();

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The caller-supplied-buffer overload must fill the caller's array.
    /// </summary>
    [Fact]
    public async Task ReadAsyncFillsACallerSuppliedBuffer()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: true);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        var buffer = new string[_expectedStrings.Length];

        // Act
        await attribute.ReadAsync(buffer);

        // Assert
        Assert.Equal(_expectedStrings, buffer);
    }

    /// <summary>
    /// A multidimensional read must respect the attribute's own dataspace.
    /// </summary>
    /// <remarks>
    /// The attribute is [2, 2, 3], so a rank-3 result must come back shaped rather than flattened - the
    /// memoryDims defaulting that the async overload has to thread through identically to the sync one.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncHonorsTheAttributeRank()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        // Act
        var actual = await attribute.ReadAsync<string[,,]>();

        // Assert
        Assert.Equal(2, actual.GetLength(0));
        Assert.Equal(2, actual.GetLength(1));
        Assert.Equal(3, actual.GetLength(2));
        Assert.Equal("001", actual[0, 0, 0]);
    }

    /// <summary>
    /// An already-cancelled token must prevent the read rather than being ignored.
    /// </summary>
    /// <remarks>
    /// Cancellation is honored at the boundary only - the decode pipeline does not thread a token
    /// through - so this is the whole of what the parameter promises, and asserting it keeps the
    /// parameter from being decorative.
    /// </remarks>
    [Fact]
    public async Task ReadAsyncObservesAnAlreadyCancelledToken()
    {
        // Arrange
        using var stream = OpenStringAttributes(suspend: false);
        using var root = H5File.Open(stream, leaveOpen: true);
        var attribute = root.Group("string").Attribute("variable");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => attribute.ReadAsync<string[]>(cancellationToken: cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => attribute.ReadAsync(new string[12], cancellationToken: cts.Token));
    }

    private static ConcurrentStream OpenStringAttributes(bool suspend)
    {
        var filePath = TestUtils.PrepareTestFile(
            H5F.libver_t.LATEST,
            fileId => TestUtils.AddString(fileId, ContainerType.Attribute));

        return new ConcurrentStream(File.ReadAllBytes(filePath), suspend);
    }
}

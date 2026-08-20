using Xunit;
using Xunit.Abstractions;

namespace PureHDF.Tests.Writing;

/// <summary>
/// The file has to be as long as the addresses inside it claim, whatever was actually written.
/// </summary>
/// <remarks>
/// An address can be referenced without ever being written to. A dataset created through
/// <see cref="H5File.BeginWrite(string, H5WriteOptions?)" /> has its payload allocated and named by its
/// layout message as soon as the structure is written, so a caller who never gets round to writing the
/// data leaves a region that everything points at and nothing has touched.
/// <para>
/// Whether that shows up as a broken file is decided by the placement, which is what makes it worth a
/// test of its own. An interleaved file tends to survive by accident: metadata is allocated after the
/// payload, and writing it extends the stream over the gap. A front-loaded file serves metadata from a
/// region at the front, so nothing extends the stream and it ends before the payload the layout points
/// at - which the HDF5 C library reports as "invalid dataset size, likely file corruption" rather than
/// as a short file.
/// </para>
/// </remarks>
public class EndOfFileAddressTests(ITestOutputHelper output)
{
    private const int ElementCount = 100_000;
    private const long PayloadBytes = ElementCount * sizeof(double);

    [SkippableTheory]
    [InlineData(H5MetadataPlacement.Interleaved)]
    [InlineData(H5MetadataPlacement.Aggregated)]
    [InlineData(H5MetadataPlacement.FrontLoaded)]
    public void AnUnwrittenDeferredPayloadIsStillCoveredByTheFile(H5MetadataPlacement placement)
    {
        // Arrange - a dataset whose payload is allocated and referenced, and which is then never
        // written. Compact layout is off so the payload cannot end up inside the object header.
        var neverWritten = new H5Dataset<double[]>(fileDims: [ElementCount]);

        var file = new H5File
        {
            ["written"] = new H5Dataset(Enumerable.Range(0, 64).ToArray()),
            ["never-written"] = neverWritten
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            using (var writer = file.BeginWrite(filePath, new H5WriteOptions
            {
                PreferCompactDatasetLayout = false,
                MetadataPlacement = placement
            }))
            {
                // Deliberately no write: the point is the payload nobody fills.
            }

            var size = new FileInfo(filePath).Length;

            output.WriteLine($"{placement,-12} file {size,10:N0} bytes, payload alone {PayloadBytes,10:N0}");

            // Assert - checkable without any external tool: the file cannot be shorter than a region
            // its own layout message points at.
            Assert.True(
                size > PayloadBytes,
                $"{placement} produced {size:N0} bytes for a file referencing {PayloadBytes:N0} bytes of "
                + $"payload, so the end-of-file address does not cover what the layout points at.");

            // And PureHDF must still see the dataset it declared.
            using (var root = H5File.OpenRead(filePath))
            {
                Assert.Equal((ulong)ElementCount, root.Dataset("never-written").Space.Dimensions[0]);
                Assert.Equal(Enumerable.Range(0, 64).ToArray(), root.Dataset("written").Read<int[]>());
            }

            // The C library is stricter than PureHDF's own reader here - it validates a contiguous
            // dataset's extent against the end of the file, which is exactly the check being satisfied.
            var dump = TestUtils.DumpH5File(filePath);

            Skip.If(dump is null, "h5dump is not available.");

            Assert.DoesNotContain("corruption", dump, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("error", dump, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("never-written", dump);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}

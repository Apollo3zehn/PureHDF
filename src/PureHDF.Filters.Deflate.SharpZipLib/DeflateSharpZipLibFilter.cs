using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PureHDF.Filters;

/// <summary>
/// Deflate filter based on SharpZipLib.
/// </summary>
public class DeflateSharpZipLibFilter : IH5Filter
{
    /// <inheritdoc />
    public H5FilterID Id => H5FilterID.Deflate;

    /// <inheritdoc />
    public string Name => "deflate";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.Buffer);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            using var decompressionStream = new InflaterInputStream(sourceStream, new Inflater(noHeader: true))
            {
                IsStreamOwner = false
            };

            using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* minimum size to expect */);
            decompressionStream.CopyTo(decompressedStream);

            return decompressedStream
                .GetBuffer()
                .AsMemory(0, (int)decompressedStream.Length);
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        throw new NotImplementedException();
    }
}
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the Deflate filter based on SharpZipLib.
/// </summary>
public static class H5DeflateSharpZipLib
{
    /// <summary>
    /// The Deflate filter function.
    /// </summary>
    /// <param name="info">The filter info.</param>
    public unsafe static Memory<byte> FilterFunction(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);

            // skip ZLIB header to get only the DEFLATE stream
            sourceStream.Seek(2, SeekOrigin.Begin);

            using var decompressionStream = new InflaterInputStream(sourceStream, new Inflater(noHeader: true))
            {
                IsStreamOwner = false
            };

            if (info.IsLast)
            {
                var resultBuffer = info.GetBuffer(info.ChunkSize /* minimum size */);
                using var decompressedStream = new MemorySpanStream(resultBuffer);

                decompressionStream.CopyTo(decompressedStream);

                return resultBuffer;
            }

            else
            {
                using var decompressedStream = new MemoryStream(info.ChunkSize /* minimum size to expect */);
                decompressionStream.CopyTo(decompressedStream);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }
}
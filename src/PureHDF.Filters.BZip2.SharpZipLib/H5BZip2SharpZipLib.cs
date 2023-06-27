using ICSharpCode.SharpZipLib.BZip2;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the BZip2 filter based on SharpZipLib.
/// </summary>
public static class H5BZip2SharpZipLib
{
    /// <summary>
    /// The BZip2 filter function.
    /// </summary>
    /// <param name="info">The filter info.</param>
    public unsafe static Memory<byte> FilterFunction(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.SourceBuffer);

            if (info.FinalBuffer.Equals(default))
            {
                using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* growable stream */);

                BZip2.Decompress(sourceStream, decompressedStream, isStreamOwner: false);

                return decompressedStream
                    .GetBuffer()
                    .AsMemory(0, (int)decompressedStream.Length);
            }

            else
            {
                using var decompressedStream = new MemorySpanStream(info.FinalBuffer);

                BZip2.Decompress(sourceStream, decompressedStream, isStreamOwner: false);

                return info.FinalBuffer;
            }
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    }
}
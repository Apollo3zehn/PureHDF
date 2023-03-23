using ICSharpCode.SharpZipLib.BZip2;

namespace PureHDF.Filters;

/// <summary>
/// Contains a function to enable support for the BZip2 filter based on SharpZipLib.
/// </summary>
public static class H5BZip2SharpZipLib
{
    /// <summary>
    /// Gets the filter function.
    /// </summary>
    public unsafe static FilterFunction FilterFunction { get; } = (flags, parameters, buffer) =>
    {
        /* We're decompressing */
        if (flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(buffer);
            using var tar = new MemoryStream();

            BZip2.Decompress(sourceStream, tar, isStreamOwner: false);

            return tar
                .GetBuffer()
                .AsMemory(0, (int)tar.Length);
        }

        /* We're compressing */
        else
        {
            throw new Exception("Writing data chunks is not yet supported by PureHDF.");
        }
    };
}
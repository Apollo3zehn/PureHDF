using ICSharpCode.SharpZipLib.BZip2;

namespace PureHDF.Tests
{
    public static class BZip2Helper
    {
        public static FilterFunc FilterFunc { get; } = (flags, parameters, buffer) =>
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
}

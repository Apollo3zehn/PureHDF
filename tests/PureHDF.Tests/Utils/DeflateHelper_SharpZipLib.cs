using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PureHDF.Tests
{
    public static class DeflateHelper_SharpZipLib
    {
        public static unsafe Memory<byte> FilterFunc(H5FilterFlags flags, uint[] parameters, Memory<byte> buffer)
        {
            /* We're decompressing */
            if (flags.HasFlag(H5FilterFlags.Decompress))
            {
                using var sourceStream = new MemorySpanStream(buffer);
                using var tar = new MemoryStream(buffer.Length /* minimum size to expect */);

                // skip ZLIB header to get only the DEFLATE stream
                sourceStream.Position = 2;

                using var decompressionStream = new InflaterInputStream(sourceStream, new Inflater(noHeader: true))
                {
                    IsStreamOwner = false
                };

                decompressionStream.CopyTo(tar);

                return tar
                    .GetBuffer()
                    .AsMemory(0, (int)tar.Length);
            }

            /* We're compressing */
            else
            {
                throw new Exception("Writing data chunks is not yet supported by PureHDF.");
            }
        }
    }
}

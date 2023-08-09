using ICSharpCode.SharpZipLib.BZip2;

namespace PureHDF.Filters;

/// <summary>
/// BZip2 filter based on SharpZipLib.
/// </summary>
public class BZip2SharpZipLibFilter : IH5Filter
{
    /// <inheritdoc />
    public H5FilterID Id => (H5FilterID)307;

    /// <inheritdoc />
    public string Name => "bzip2";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.Buffer);
            using var decompressedStream = new MemoryStream(capacity: info.ChunkSize /* minimum size to expect */);

            BZip2.Decompress(sourceStream, decompressedStream, isStreamOwner: false);

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
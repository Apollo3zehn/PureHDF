using ICSharpCode.SharpZipLib.BZip2;

namespace PureHDF.Filters;

/// <summary>
/// BZip2 filter based on SharpZipLib.
/// </summary>
public class BZip2SharpZipLibFilter : IH5Filter
{
    /// <summary>
    /// The block size options key. The block size must be in the range of 1 (fastest) to 9 (best) and the default is 9.
    /// </summary>
    public const string BLOCK_SIZE = "block-size";

    /// <summary>
    /// The BZip2 filter identifier.
    /// </summary>
    public const ushort Id = 307;

    /// <inheritdoc />
    public ushort FilterId => Id;

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
            using var sourceStream = new MemorySpanStream(info.Buffer);
            using var compressedStream = new MemoryStream(capacity: info.ChunkSize /* maximum size to expect */);

            var blockSize = (int)info.Parameters[0];

            using (var compressionStream = new BZip2OutputStream(compressedStream, blockSize)
            {
                IsStreamOwner = false
            })
            {
                sourceStream.CopyTo(compressionStream);
            };

            return compressedStream
                .GetBuffer()
                .AsMemory(0, (int)compressedStream.Length);
        }
    }

    /// <inheritdoc />
    public uint[] GetParameters(uint[] chunkDimensions, uint typeSize, Dictionary<string, object>? options)
    {
        // block size parameter:    https://github.com/nexusformat/HDF5-External-Filter-Plugins/blob/49e3b65eca772bca77af13ba047d8b577673afba/BZIP2/src/H5Zbzip2.c#L176
        // block size parameter:    https://github.com/PyTables/PyTables/blob/2db63857e1f01d62414c40f75911337c29c246f7/src/H5Zbzip2.c#L153
        // default = 2:             https://github.com/nexusformat/HDF5-External-Filter-Plugins/blob/49e3b65eca772bca77af13ba047d8b577673afba/BZIP2/example/h5ex_d_bzip2.c#L51
        // default = 9:             https://github.com/PyTables/PyTables/blob/2db63857e1f01d62414c40f75911337c29c246f7/src/H5Zbzip2.c#L151
        // default = 9:             man bzip2
        // => we choose 9 as the default

        var blockSize = GetBlockSizeValue(options);

        if (blockSize < 1 || blockSize > 9)
            throw new Exception("The block size must be in the range of 1..9.");

        return new uint[] { (uint)blockSize };
    }

    private static int GetBlockSizeValue(Dictionary<string, object>? options)
    {
        if (
            options is not null &&
            options.TryGetValue(BLOCK_SIZE, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{BLOCK_SIZE}' must be of type {nameof(Int32)}.");
        }

        else
        {
            return 9;
        }
    }
}
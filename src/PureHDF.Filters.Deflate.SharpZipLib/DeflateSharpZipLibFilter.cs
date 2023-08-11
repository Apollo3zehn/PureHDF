﻿using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PureHDF.Filters;

/// <summary>
/// Deflate filter based on SharpZipLib.
/// </summary>
public class DeflateSharpZipLibFilter : IH5Filter
{
    /// <summary>
    /// The compression level options key.
    /// </summary>
    public const string COMPRESSION_LEVEL = "compression-level";

    /// <summary>
    /// The deflate filter identifier.
    /// </summary>
    public const ushort Id = DeflateFilter.Id;

    /// <inheritdoc />
    public ushort FilterId => Id;

    /// <inheritdoc />
    public string Name => "deflate";

    /// <inheritdoc />
    public Memory<byte> Filter(FilterInfo info)
    {
        /* We're decompressing */
        if (info.Flags.HasFlag(H5FilterFlags.Decompress))
        {
            using var sourceStream = new MemorySpanStream(info.Buffer);

            using var decompressionStream = new InflaterInputStream(sourceStream)
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
            using var sourceStream = new MemorySpanStream(info.Buffer);
            using var compressedStream = new MemoryStream(capacity: info.ChunkSize /* maximum size to expect */);

            var compressionLevel = (int)info.Parameters[0];

            /* workaround for https://forum.hdfgroup.org/t/is-deflate-filter-compression-level-1-default-supported/11416 */
            if (compressionLevel == 6)
                compressionLevel = -1;

            using (var compressionStream = new DeflaterOutputStream(
                compressedStream,
                new Deflater(compressionLevel))
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
    public uint[] GetParameters(H5Dataset dataset, uint typeSize, Dictionary<string, object>? options)
    {
        var value = GetCompressionLevelValue(options);

        /* workaround for https://forum.hdfgroup.org/t/is-deflate-filter-compression-level-1-default-supported/11416 */
        if (value == -1)
            value = 6;

        return new uint[] { unchecked((uint)value) };
    }

    private static int GetCompressionLevelValue(Dictionary<string, object>? options)
    {
        if (
            options is not null && 
            options.TryGetValue(COMPRESSION_LEVEL, out var objectValue))
        {
            if (objectValue is int value)
                return value;

            else
                throw new Exception($"The value of the filter parameter '{COMPRESSION_LEVEL}' must be of type System.Int32.");
        }

        else
        {
            return -1;
        }
    }
}
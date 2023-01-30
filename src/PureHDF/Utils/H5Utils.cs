using System.Runtime.CompilerServices;
using System.Text;

namespace PureHDF
{
    internal static class H5Utils
    {
        public static void SwizzleCoords(ulong[] swizzledCoords, int unlimitedDim)
        {
            /* Nothing to do when unlimited dimension is at position 0 */
            if (unlimitedDim > 0)
            {
                var tmp = swizzledCoords[unlimitedDim];

                for (int i = unlimitedDim; i > 0; i++)
                {
                    swizzledCoords[i] = swizzledCoords[i - 1];
                }

                swizzledCoords[0] = tmp;
            }
        }

        public static ulong ToLinearIndex(this Span<ulong> coordinates, ulong[] dimensions)
        {
            var linearIndex = 0UL;
            var rank = coordinates.Length;

            if (dimensions.Length != rank)
                throw new Exception("Rank of coordinates and dimensions arrays must be equal.");

            for (int i = 0; i < rank; i++)
            {
                linearIndex = linearIndex * dimensions[i] + coordinates[i];
            }

            return linearIndex;
        }

        public static ulong ToLinearIndexPrecomputed(this ulong[] coordinates, ulong[] totalSize)
        {
            // H5VM.c (H5VM_array_offset_pre)
            var linearIndex = 0UL;
            var rank = coordinates.Length;

            if (totalSize.Length != rank)
                throw new Exception("Rank of coordinates and total size arrays must be equal.");

            /* Compute offset in array */
            for (int i = 0; i < rank; i++)
            {
                linearIndex += totalSize[i] * coordinates[i];
            }

            return linearIndex;
        }

        public static ulong[] ToCoordinates(this ulong linearIndex, ulong[] dimensions)
        {
            var rank = dimensions.Length;
            var coordinates = new ulong[rank];

            for (int i = rank - 1; i >= 0; i--)
            {
                linearIndex = (ulong)Math.DivRem((long)linearIndex, (long)dimensions[i], out var coordinate);
                coordinates[i] = (ulong)coordinate;
            }

            return coordinates;
        }

        public static ulong[] AccumulateReverse(this ulong[] totalSize)
        {
            var result = new ulong[totalSize.Length];
            var acc = 1UL;

            for (int i = totalSize.Length - 1; i >= 0; i--)
            {
                result[i] = acc;
                acc *= totalSize[i];
            }

            return result;
        }

        // H5VMprivate.h (H5VM_bit_get)
        public static byte[] SequentialBitMask { get; } = new byte[] { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VectorCompare(byte rank, ulong[] v1, ulong[] v2)
        {
            for (int i = 0; i < rank; i++)
            {
                if (v1[i] < v2[i])
                    return -1;

                if (v1[i] > v2[i])
                    return 1;
            }

            return 0;
        }

        public static uint ComputeChunkSizeLength(ulong chunkSize)
        {
            // H5Dearray.c (H5D__earray_crt_context)
            /* Compute the size required for encoding the size of a chunk, allowing
             *      for an extra byte, in case the filter makes the chunk larger.
             */
            var chunkSizeLength = 1 + ((uint)Math.Log(chunkSize, 2) + 8) / 8;

            if (chunkSizeLength > 8)
                chunkSizeLength = 8;

            return chunkSizeLength;
        }

        public static void ValidateSignature(byte[] actual, byte[] expected)
        {
            var actualString = Encoding.ASCII.GetString(actual);
            var expectedString = Encoding.ASCII.GetString(expected);

            if (actualString != expectedString)
                throw new Exception($"The actual signature '{actualString}' does not match the expected signature '{expectedString}'.");
        }

        public static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong FloorDiv(ulong x, ulong y)
        {
            return x / y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CeilDiv(ulong x, ulong y)
        {
            return x % y == 0 ? x / y : x / y + 1;
        }

        public static ulong FindMinByteCount(ulong value)
        {
            ulong lg_v = 1;

            while ((value >>= 1) != 0)
            {
                lg_v++;
            }

            ulong result = lg_v >> 3;

            if (lg_v != result << 3)
                result += 1;

            return result;
        }

        public static void EnsureEndianness(Span<byte> source, Span<byte> destination, ByteOrder byteOrder, uint bytesOfType)
        {
            if (byteOrder == ByteOrder.VaxEndian)
                throw new Exception("VAX-endian byte order is not supported.");

            var isLittleEndian = BitConverter.IsLittleEndian;

            if ((isLittleEndian && byteOrder != ByteOrder.LittleEndian) ||
               (!isLittleEndian && byteOrder != ByteOrder.BigEndian))
            {
                EndiannessConverter.Convert((int)bytesOfType, source, destination);
            }
        }

        public static ulong CalculateSize(IEnumerable<uint> dimensionSizes, DataspaceType type = DataspaceType.Simple)
        {
            return H5Utils.CalculateSize(dimensionSizes.Select(value => (ulong)value), type);
        }

        public static ulong CalculateSize(IEnumerable<ulong> dimensionSizes, DataspaceType type = DataspaceType.Simple)
        {
            switch (type)
            {
                case DataspaceType.Scalar:
                    return 1;

                case DataspaceType.Simple:

                    var totalSize = 0UL;

                    if (dimensionSizes.Any())
                        totalSize = dimensionSizes.Aggregate((x, y) => x * y);

                    return totalSize;

                case DataspaceType.Null:
                    return 0;

                default:
                    throw new Exception($"The dataspace type '{type}' is not supported.");
            }
        }

        public static string ConstructExternalFilePath(H5File file, string filePath, H5LinkAccess linkAccess)
        {
            // h5Fint.c (H5F_prefix_open_file)
            // reference: https://support.hdfgroup.org/HDF5/doc/RM/H5L/H5Lcreate_external.htm

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The external file path is not a valid URI.");

            // absolute
            if (uri.IsAbsoluteUri)
            {
                if (File.Exists(filePath))
                    return filePath;
            }
            // relative
            else
            {
                // prefixes
                var envVariable = Environment
                    .GetEnvironmentVariable("HDF5_EXT_PREFIX");

                if (envVariable is not null)
                {
                    // cannot work on Windows
                    //var envPrefixes = envVariable.Split(":");

                    //foreach (var envPrefix in envPrefixes)
                    //{
                    //    var envResult = PathCombine(envPrefix, externalFilePath);

                    //    if (File.Exists(envResult))
                    //        return envResult;
                    //}

                    var envResult = PathCombine(envVariable, filePath);

                    if (File.Exists(envResult))
                        return envResult;
                }

                // link access property list
                if (!string.IsNullOrWhiteSpace(linkAccess.ExternalLinkPrefix))
                {
                    var propPrefix = linkAccess.ExternalLinkPrefix;
                    var propResult = PathCombine(propPrefix, filePath);

                    if (File.Exists(propResult))
                        return propResult;
                }

                // relative to this file
                var filePrefix = Path.GetDirectoryName(file.Path);

                var fileResult = filePrefix is null
                    ? filePath
                    : PathCombine(filePrefix, filePath);

                if (File.Exists(fileResult))
                    return fileResult;

                // relative to current directory
                var cdResult = Path.GetFullPath(filePath);

                if (File.Exists(cdResult))
                    return cdResult;
            }

            throw new Exception($"Unable to open external file '{filePath}'.");

            // helper
            static string PathCombine(string prefix, string relativePath)
            {
                try
                {
                    return Path.Combine(prefix, relativePath);
                }
                catch (Exception)
                {
                    throw new Exception("Unable to construct absolute file path for external file.");
                }
            }
        }

        public static string ConstructExternalFilePath(string filePath, H5DatasetAccess datasetAccess)
        {
            // H5system.c (H5_combine_path)

            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                throw new Exception("The external file path is not a valid URI.");

            // absolute
            if (uri.IsAbsoluteUri)
            {
                return filePath;
            }
            
            // relative
            else
            {
                // dataset access property list
                if (!string.IsNullOrWhiteSpace(datasetAccess.ExternalFilePrefix))
                {
                    var propPrefix = datasetAccess.ExternalFilePrefix;

                    var propResult = propPrefix is null
                        ? filePath
                        : PathCombine(propPrefix, filePath);

                    return propResult;
                }

                return filePath;
            }

            // helper
            static string PathCombine(string prefix, string relativePath)
            {
                try
                {
                    return Path.Combine(prefix, relativePath);
                }
                catch (Exception)
                {
                    throw new Exception("Unable to construct absolute file path for external file.");
                }
            }
        }

        public static ulong ReadUlong(H5BaseReader reader, ulong size)
        {
            return size switch
            {
                1 => reader.ReadByte(),
                2 => reader.ReadUInt16(),
                4 => reader.ReadUInt32(),
                8 => reader.ReadUInt64(),
                _ => ReadUlongArbitrary(reader, size)
            };
        }

        private static ulong ReadUlongArbitrary(H5BaseReader reader, ulong size)
        {
            var result = 0UL;
            var shift = 0;

            for (ulong i = 0; i < size; i++)
            {
                var value = reader.ReadByte();
                result += (ulong)(value << shift);
                shift += 8;
            }

            return result;
        }
    }
}

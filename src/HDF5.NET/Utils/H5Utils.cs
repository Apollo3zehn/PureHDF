using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace HDF5.NET
{
    internal static class H5Utils
    {
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

        public static unsafe T[] ReadCompound<T>(DatatypeMessage datatype,
                                                 DataspaceMessage dataspace,
                                                 Superblock superblock,
                                                 Span<byte> data,
                                                 Func<FieldInfo, string> getName) where T : struct
        {
            if (datatype.Class != DatatypeMessageClass.Compound)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Compound}'.");

            var type = typeof(T);
            var fieldInfoMap = new Dictionary<string, FieldProperties>();

            foreach (var fieldInfo in type.GetFields())
            {
                var name = getName(fieldInfo);

                var isNotSupported = H5Utils.IsReferenceOrContainsReferences(fieldInfo.FieldType)
                                  && fieldInfo.FieldType != typeof(string);

                if (isNotSupported)
                    throw new Exception("Nested nullable fields are not supported.");

                fieldInfoMap[name] = new FieldProperties()
                {
                    FieldInfo = fieldInfo,
                    Offset = Marshal.OffsetOf(type, fieldInfo.Name)
                };
            }

            var properties = datatype.Properties
                .Cast<CompoundPropertyDescription>()
                .ToList();

            var sourceOffset = 0UL;
            var sourceRawBytes = data;
            var sourceElementSize = datatype.Size;

            var targetArraySize = H5Utils.CalculateSize(dataspace.DimensionSizes, dataspace.Type);
            var targetArray = new T[targetArraySize];
            var targetElementSize = Marshal.SizeOf<T>();

            for (int i = 0; i < targetArray.Length; i++)
            {
                var targetRawBytes = new byte[targetElementSize];
                var stringMap = new Dictionary<FieldProperties, string>();

                foreach (var property in properties)
                {
                    if (!fieldInfoMap.TryGetValue(property.Name, out var fieldInfo))
                        throw new Exception($"The property named '{property.Name}' in the HDF5 data type does not exist in the provided structure of type '{typeof(T)}'.");

                    var fieldSize = (int)property.MemberTypeMessage.Size;

                    // strings
                    if (fieldInfo.FieldInfo.FieldType == typeof(string))
                    {
                        var sourceIndex = (int)(sourceOffset + property.MemberByteOffset);
                        var sourceIndexEnd = sourceIndex + fieldSize;
                        var targetIndex = fieldInfo.Offset.ToInt64();
                        var value = H5Utils.ReadString(property.MemberTypeMessage, sourceRawBytes[sourceIndex..sourceIndexEnd], superblock);

                        stringMap[fieldInfo] = value[0];
                    }
                    // other value types
                    else
                    {
                        for (uint j = 0; j < fieldSize; j++)
                        {
                            var sourceIndex = sourceOffset + property.MemberByteOffset + j;
                            var targetIndex = fieldInfo.Offset.ToInt64() + j;

                            targetRawBytes[targetIndex] = sourceRawBytes[(int)sourceIndex];
                        }
                    }
                }

                sourceOffset += sourceElementSize;

                fixed (byte* ptr = targetRawBytes.AsSpan())
                {
                    // http://benbowen.blog/post/fun_with_makeref/
                    // https://stackoverflow.com/questions/4764573/why-is-typedreference-behind-the-scenes-its-so-fast-and-safe-almost-magical
                    // Both do not work because struct layout is different with __makeref:
                    // https://stackoverflow.com/questions/1918037/layout-of-net-value-type-in-memory
                    targetArray[i] = Marshal.PtrToStructure<T>(new IntPtr(ptr));

                    foreach (var entry in stringMap)
                    {
                        var reference = __makeref(targetArray[i]);
                        entry.Key.FieldInfo.SetValueDirect(reference, entry.Value);
                    }
                }
            }

            return targetArray;
        }

        public static string[] ReadString(DatatypeMessage datatype, Span<byte> data, Superblock superblock)
        {
            var isFixed = datatype.Class == DatatypeMessageClass.String;

            if (!isFixed && datatype.Class != DatatypeMessageClass.VariableLength)
                throw new Exception($"Attribute data type class '{datatype.Class}' cannot be read as string.");

            var size = (int)datatype.Size;
            var result = new List<string>();

            if (isFixed)
            {
                var bitField = datatype.BitField as StringBitFieldDescription;

                if (bitField == null)
                    throw new Exception("String bit field desciption must not be null.");

                if (bitField.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                var position = 0;

                while (position != data.Length)
                {
#error Fixed-length string with null terminate padding is not read in correctly. The string is still padded with zero or more \0 characters.
                    var value = H5Utils.ReadFixedLengthString(data[position..(position + size)]);
                    result.Add(value);
                    position += size;
                }
            }
            else
            {
                var bitField = datatype.BitField as VariableLengthBitFieldDescription;

                if (bitField == null)
                    throw new Exception("Variable-length bit field desciption must not be null.");

                if (bitField.Type != VariableLengthType.String)
                    throw new Exception($"Variable-length type must be '{VariableLengthType.String}'.");

                if (bitField.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                // see IV.B. Disk Format: Level 2B - Data Object Data Storage
                using (var dataReader = new H5BinaryReader(new MemoryStream(data.ToArray())))
                {
                    while (dataReader.BaseStream.Position != data.Length)
                    {
                        var dataSize = dataReader.ReadUInt32(); // for what do we need this?
                        var globalHeapId = new GlobalHeapId(dataReader, superblock);
                        var globalHeapCollection = globalHeapId.Collection;
                        var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                        var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);

                        result.Add(value);
                    }
                }
            }

            return result.ToArray();
        }

        public static string ReadFixedLengthString(Span<byte> data, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
        }

        public static string ReadFixedLengthString(H5BinaryReader reader, int length, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            var data = reader.ReadBytes(length);

            return encoding switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
        }

        public static string ReadNullTerminatedString(H5BinaryReader reader, bool pad, int padSize = 8, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            var data = new List<byte>();
            var byteValue = reader.ReadByte();

            while (byteValue != '\0')
            {
                data.Add(byteValue);
                byteValue = reader.ReadByte();
            }

            var result = encoding switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };

            if (pad)
            {
                // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
                var paddingCount = (padSize - (result.Length + 1) % padSize) % padSize;
                reader.BaseStream.Seek(paddingCount, SeekOrigin.Current);
            }

            return result;
        }

        public static ulong ReadUlong(H5BinaryReader reader, ulong size)
        {
            return size switch
            {
                1 => reader.ReadByte(),
                2 => reader.ReadUInt16(),
                4 => reader.ReadUInt32(),
                8 => reader.ReadUInt64(),
                _ => H5Utils.ReadUlongArbitrary(reader, size)
            };
        }

        public static bool IsReferenceOrContainsReferences(Type type)
        {
            var name = nameof(RuntimeHelpers.IsReferenceOrContainsReferences);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;
            var method = typeof(RuntimeHelpers).GetMethod(name, flags);
            var generic = method.MakeGenericMethod(type);

            return (bool)generic.Invoke(null, null);
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

                    var byteSize = 0UL;

                    if (dimensionSizes.Any())
                        byteSize = dimensionSizes.Aggregate((x, y) => x * y);

                    return byteSize;

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

                if (envVariable != null)
                {
                    // cannot work in Windows
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
                var fileResult = PathCombine(filePrefix, filePath);

                if (File.Exists(fileResult))
                    return fileResult;

                // relative to current directory
                var cdResult = Path.GetFullPath(filePath);

                if (File.Exists(cdResult))
                    return cdResult;
            }

            throw new Exception($"Unable to open external file '{filePath}'.");

            // helper
            string PathCombine(string prefix, string relativePath)
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
                    var propResult = PathCombine(propPrefix, filePath);

                    return propResult;
                }

                return filePath;
            }

            // helper
            string PathCombine(string prefix, string relativePath)
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

        private static ulong ReadUlongArbitrary(H5BinaryReader reader, ulong size)
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

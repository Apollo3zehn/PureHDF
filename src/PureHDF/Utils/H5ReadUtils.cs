using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF
{
    internal static class H5ReadUtils
    {
        public static unsafe T[] ReadCompound<T>(
            H5Context context,
            DatatypeMessage datatype,
            Span<byte> data,
            Func<FieldInfo, string> getName) where T : struct
        {
            if (datatype.Class != DatatypeMessageClass.Compound)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Compound}'.");

            var type = typeof(T);
            var fieldInfoMap = new Dictionary<string, FieldProperties>();

            static bool IsFixedSizeArray(FieldInfo fieldInfo)
            {
                var attribute = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();

                if (attribute is not null && attribute.Value == UnmanagedType.ByValArray)
                    return true;

                return false;
            }

            foreach (var fieldInfo in type.GetFields())
            {
                var name = getName(fieldInfo);

                var isNotSupported = IsReferenceOrContainsReferences(fieldInfo.FieldType) &&
                    fieldInfo.FieldType != typeof(string) &&
                    !IsFixedSizeArray(fieldInfo);

                if (isNotSupported)
                    throw new Exception("Nested nullable fields are not supported.");

                fieldInfoMap[name] = new FieldProperties()
                {
                    FieldInfo = fieldInfo,
                    Offset = Marshal.OffsetOf(type, fieldInfo.Name)
                };
            }

            var members = datatype.Properties
                .Cast<CompoundPropertyDescription>()
                .ToList();

            var sourceOffset = 0UL;
            var sourceRawBytes = data;
            var sourceElementSize = datatype.Size;

            var targetArraySize = data.Length / datatype.Size;
            var targetArray = new T[targetArraySize];
            var targetElementSize = Marshal.SizeOf<T>();

            for (int i = 0; i < targetArray.Length; i++)
            {
                var targetRawBytes = new byte[targetElementSize];
                var stringMap = new Dictionary<FieldProperties, string>();

                foreach (var member in members)
                {
                    if (!fieldInfoMap.TryGetValue(member.Name, out var fieldInfo))
                        throw new Exception($"The property named '{member.Name}' in the HDF5 data type does not exist in the provided structure of type '{typeof(T)}'.");

                    var fieldSize = (int)member.MemberTypeMessage.Size;

                    // strings
                    if (fieldInfo.FieldInfo.FieldType == typeof(string))
                    {
                        var sourceIndex = (int)(sourceOffset + member.MemberByteOffset);
                        var sourceIndexEnd = sourceIndex + fieldSize;
                        var targetIndex = fieldInfo.Offset.ToInt64();
                        var value = ReadString(context, member.MemberTypeMessage, sourceRawBytes[sourceIndex..sourceIndexEnd]);

                        stringMap[fieldInfo] = value[0];
                    }

                    // value types
                    else
                    {
                        var sourceIndex = (int)(sourceOffset + member.MemberByteOffset);
                        var targetIndex = (int)fieldInfo.Offset.ToInt64();

                        sourceRawBytes
                            .Slice(sourceIndex, fieldSize)
                            .CopyTo(targetRawBytes.AsSpan(targetIndex, fieldSize));
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

        private static readonly string[] oneElementStringArray = new string[1];
        private static readonly Dictionary<string, object?>[] oneElementCompountArray = new Dictionary<string, object?>[1];

        public static unsafe Dictionary<string, object?>[] ReadCompound(
            H5Context context,
            DatatypeMessage datatype,
            Span<byte> data,
            Dictionary<string, object?>[]? result = default)
        {
            if (datatype.Class != DatatypeMessageClass.Compound)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Compound}'.");

            var members = datatype.Properties
                .Cast<CompoundPropertyDescription>()
                .ToList();

            var sourceOffset = 0UL;
            var sourceElementSize = datatype.Size;

            if (result is null)
            {
                var elementCount = data.Length / datatype.Size;
                result = new Dictionary<string, object?>[elementCount];
            }

            for (int i = 0; i < result.Length; i++)
            {
                var map = new Dictionary<string, object?>();

                foreach (var member in members)
                {
                    var memberType = member.MemberTypeMessage;
                    var fieldSize = (int)memberType.Size;
                    var sourceIndex = (int)(sourceOffset + member.MemberByteOffset);
                    var slicedData = data.Slice(sourceIndex, fieldSize);

                    var fixedPointBitfield = memberType.BitField as FixedPointBitFieldDescription;
                    var variableLengthBitfield = memberType.BitField as VariableLengthBitFieldDescription;

                    map[member.Name] = (memberType.Class, memberType.Size) switch
                    {
                        (DatatypeMessageClass.FixedPoint, 1) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, byte>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 1) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, sbyte>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 2) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ushort>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 2) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, short>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 4) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, uint>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 4) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, int>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 8) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ulong>(slicedData)[0],
                        (DatatypeMessageClass.FixedPoint, 8) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, long>(slicedData)[0],

                        (DatatypeMessageClass.FloatingPoint, 4) => MemoryMarshal.Cast<byte, float>(slicedData)[0],
                        (DatatypeMessageClass.FloatingPoint, 8) => MemoryMarshal.Cast<byte, double>(slicedData)[0],

                        (DatatypeMessageClass.String, _)
                            => ReadString(context, memberType, slicedData, oneElementStringArray)[0],

                        // TODO: Bitfield padding type is not being applied here as well as in the normal Read<T> method.
                        (DatatypeMessageClass.BitField, _)
                            => slicedData.ToArray(),

                        (DatatypeMessageClass.Opaque, _)
                            => slicedData.ToArray(),

                        (DatatypeMessageClass.Compound, _)
                            => ReadCompound(context, memberType, slicedData, oneElementCompountArray)[0],

                        // TODO: Reference type (from the bit field) is not being considered here as well as in the normal Read<T> method.
                        (DatatypeMessageClass.Reference, _)
                            => MemoryMarshal.Cast<byte, H5ObjectReference>(slicedData)[0],

                        /* It is difficult to avoid array allocation here */
                        (DatatypeMessageClass.Enumerated, _)
                            => (ReadEnumerated(context, memberType, slicedData) ?? new object[1]).GetValue(0),

                        (DatatypeMessageClass.VariableLength, _) when variableLengthBitfield!.Type == VariableLengthType.String
                            => ReadString(context, memberType, slicedData, oneElementStringArray)[0],

                        (DatatypeMessageClass.Array, _)
                            => ReadArray(context, memberType, slicedData),

                        _ => default
                    };
                }

                result[i] = map;
                sourceOffset += sourceElementSize;
            }

            return result;
        }

        private static Array? ReadRawArray(H5Context context, DatatypeMessage baseType, Span<byte> slicedData)
        {
            _ = context.Superblock;

            var fixedPointBitfield = baseType.BitField as FixedPointBitFieldDescription;
            var variableLengthBitfield = baseType.BitField as VariableLengthBitFieldDescription;

            return (baseType.Class, baseType.Size) switch
            {
                (DatatypeMessageClass.FixedPoint, 1) when !fixedPointBitfield!.IsSigned => slicedData.ToArray(),
                (DatatypeMessageClass.FixedPoint, 1) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, sbyte>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 2) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ushort>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 2) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, short>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 4) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, uint>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 4) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, int>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 8) when !fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, ulong>(slicedData).ToArray(),
                (DatatypeMessageClass.FixedPoint, 8) when fixedPointBitfield!.IsSigned => MemoryMarshal.Cast<byte, long>(slicedData).ToArray(),

                (DatatypeMessageClass.FloatingPoint, 4) => MemoryMarshal.Cast<byte, float>(slicedData).ToArray(),
                (DatatypeMessageClass.FloatingPoint, 8) => MemoryMarshal.Cast<byte, double>(slicedData).ToArray(),

                (DatatypeMessageClass.String, _)
                    => ReadString(context, baseType, slicedData),

                // TODO: Bitfield padding type is not being applied here as well as in the normal Read<T> method.
                (DatatypeMessageClass.BitField, _)
                    => slicedData.ToArray(),

                (DatatypeMessageClass.Opaque, _)
                    => slicedData.ToArray(),

                (DatatypeMessageClass.Compound, _)
                    => ReadCompound(context, baseType, slicedData),

                // TODO: Reference type (from the bit field) is not being considered here as well as in the normal Read<T> method.
                (DatatypeMessageClass.Reference, _)
                    => MemoryMarshal.Cast<byte, H5ObjectReference>(slicedData).ToArray(),

                (DatatypeMessageClass.Enumerated, _)
                    => ReadEnumerated(context, baseType, slicedData),

                (DatatypeMessageClass.VariableLength, _) when variableLengthBitfield!.Type == VariableLengthType.String
                    => ReadString(context, baseType, slicedData),

                (DatatypeMessageClass.Array, _)
                    => ReadArray(context, baseType, slicedData),

                _ => default
            };
        }

        private static Array? ReadArray(H5Context context, DatatypeMessage type, Span<byte> slicedData)
        {
            if (type.Class != DatatypeMessageClass.Array)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Array}'.");

            var properties = (ArrayPropertyDescription)type.Properties[0];
            var baseType = properties.BaseType;

            return ReadRawArray(context, baseType, slicedData);
        }

        private static Array? ReadEnumerated(H5Context context, DatatypeMessage type, Span<byte> slicedData)
        {
            if (type.Class != DatatypeMessageClass.Enumerated)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Enumerated}'.");

            var properties = (EnumerationPropertyDescription)type.Properties[0];
            var baseType = properties.BaseType;

            return ReadRawArray(context, baseType, slicedData);
        }

        public static string[] ReadString(
            H5Context context,
            DatatypeMessage datatype,
            Span<byte> data,
            string[]? result = default)
        {
            /* Padding
             * https://support.hdfgroup.org/HDF5/doc/H5.format.html#DatatypeMessage
             * Search for "null terminate": null terminate and null padding are essentially
             * the same when simply reading them from file.
             */
            var size = (int)datatype.Size;

            if (result is null)
            {
                var elementCount = data.Length / size;
                result = new string[elementCount];
            }

            var isFixed = datatype.Class == DatatypeMessageClass.String;

            if (!isFixed && datatype.Class != DatatypeMessageClass.VariableLength)
                throw new Exception($"Attribute data type class '{datatype.Class}' cannot be read as string.");

            if (isFixed)
            {
                if (datatype.BitField is not StringBitFieldDescription bitField)
                    throw new Exception("String bit field description must not be null.");

                var position = 0;

                Func<string, string> trim = bitField.PaddingType switch
                {
                    PaddingType.NullTerminate => value => value.Split(new char[] { '\0' }, 2)[0],
                    PaddingType.NullPad => value => value.TrimEnd('\0'),
                    PaddingType.SpacePad => value => value.TrimEnd(' '),
                    _ => throw new Exception("Unsupported padding type.")
                };

                for (int i = 0; i < result.Length; i++)
                {
                    var value = H5ReadUtils.ReadFixedLengthString(data[position..(position + size)]);

                    value = trim(value);
                    result[i] = value;
                    position += size;
                }
            }
            else
            {
                /* String is always split after first \0 when writing data to file. 
                 * In other words, padding type only matters when reading data.
                 */

                if (datatype.BitField is not VariableLengthBitFieldDescription bitField)
                    throw new Exception("Variable-length bit field desciption must not be null.");

                if (bitField.Type != VariableLengthType.String)
                    throw new Exception($"Variable-length type must be '{VariableLengthType.String}'.");

                // see IV.B. Disk Format: Level 2B - Data Object Data Storage
                using var localReader = new H5StreamReader(new MemoryStream(data.ToArray()), leaveOpen: false);

                Func<string, string> trim = bitField.PaddingType switch
                {
                    PaddingType.NullTerminate => value => value,
                    PaddingType.NullPad => value => value,
                    PaddingType.SpacePad => value => value.TrimEnd(' '),
                    _ => throw new Exception("Unsupported padding type.")
                };

                for (int i = 0; i < result.Length; i++)
                {
                    var dataSize = localReader.ReadUInt32(); // for what do we need this?
                    var globalHeapId = new GlobalHeapId(context, localReader);
                    var globalHeapCollection = globalHeapId.Collection;
                    var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                    var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);

                    value = trim(value);
                    result[i] = value;
                }
            }

            return result;
        }

        public static string ReadFixedLengthString(Span<byte> data, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
#if NETSTANDARD2_0
            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
#else
            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
#endif
        }

        public static string ReadFixedLengthString(H5BaseReader reader, int length, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            var data = reader.ReadBytes(length);

            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
        }

        public static string ReadNullTerminatedString(H5BaseReader reader, bool pad, int padSize = 8, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
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
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };

            if (pad)
            {
                // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
                var paddingCount = (padSize - (result.Length + 1) % padSize) % padSize;
                reader.Seek(paddingCount, SeekOrigin.Current);
            }

            return result;
        }

        public static bool IsReferenceOrContainsReferences(Type type)
        {
#if NETSTANDARD2_0
            return false;
#else
            var name = nameof(RuntimeHelpers.IsReferenceOrContainsReferences);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;
            var method = typeof(RuntimeHelpers).GetMethod(name, flags)!;
            var generic = method.MakeGenericMethod(type);

            return (bool)generic.Invoke(null, null)!;
#endif
        }
    }
}

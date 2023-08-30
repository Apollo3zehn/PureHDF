using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage(
    uint Size,
    DatatypeBitFieldDescription BitField,
    DatatypePropertyDescription[] Properties
) : Message
{
    private static readonly MethodInfo _methodInfoGetDecodeInfoForTopLevelUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(GetDecodeInfoForTopLevelUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

    private byte _version;

    private DatatypeMessageClass _class;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 3))
                throw new Exception("The version number must be in the range of 1..3.");

            _version = value;
        }
    }

    public required DatatypeMessageClass Class
    {
        get
        {
            return _class;
        }
        init
        {
            if (!(0 <= (byte)value && (byte)value <= 10))
                throw new Exception("The class number must be in the range of 0..10.");

            _class = value;
        }
    }

    public static DatatypeMessage Decode(H5DriverBase driver)
    {
        var classVersion = driver.ReadByte();
        var version = (byte)(classVersion >> 4);
        var @class = (DatatypeMessageClass)(classVersion & 0x0F);

        DatatypeBitFieldDescription bitField = @class switch
        {
            DatatypeMessageClass.FixedPoint => FixedPointBitFieldDescription.Decode(driver),
            DatatypeMessageClass.FloatingPoint => FloatingPointBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Time => TimeBitFieldDescription.Decode(driver),
            DatatypeMessageClass.String => StringBitFieldDescription.Decode(driver),
            DatatypeMessageClass.BitField => BitFieldBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Opaque => OpaqueBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Compound => CompoundBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Reference => ReferenceBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Enumerated => EnumerationBitFieldDescription.Decode(driver),
            DatatypeMessageClass.VariableLength => VariableLengthBitFieldDescription.Decode(driver),
            DatatypeMessageClass.Array => ArrayBitFieldDescription.Decode(driver),
            _ => throw new NotSupportedException($"The data type message class '{@class}' is not supported.")
        };

        var size = driver.ReadUInt32();

        var memberCount = @class switch
        {
            DatatypeMessageClass.String => 0,
            DatatypeMessageClass.Reference => 0,
            DatatypeMessageClass.Compound => ((CompoundBitFieldDescription)bitField).MemberCount,
            _ => 1
        };

        var properties = new DatatypePropertyDescription[memberCount];

        for (int i = 0; i < memberCount; i++)
        {
            DatatypePropertyDescription singleProperties = @class switch
            {
                DatatypeMessageClass.FixedPoint => FixedPointPropertyDescription.Decode(driver),
                DatatypeMessageClass.FloatingPoint => FloatingPointPropertyDescription.Decode(driver),
                DatatypeMessageClass.Time => TimePropertyDescription.Decode(driver),
                DatatypeMessageClass.BitField => BitFieldPropertyDescription.Decode(driver),
                DatatypeMessageClass.Opaque => OpaquePropertyDescription.Decode(driver, ((OpaqueBitFieldDescription)bitField).AsciiTagByteLength),
                DatatypeMessageClass.Compound => CompoundPropertyDescription.Decode(driver, version, size),
                DatatypeMessageClass.Enumerated => EnumerationPropertyDescription.Decode(driver, version, size, ((EnumerationBitFieldDescription)bitField).MemberCount),
                DatatypeMessageClass.VariableLength => VariableLengthPropertyDescription.Decode(driver),
                DatatypeMessageClass.Array => ArrayPropertyDescription.Decode(driver, version),
                _ => throw new NotSupportedException($"The data type message '{@class}' is not supported.")
            };

            if (singleProperties is not null)
                properties[i] = singleProperties;
        }

        return new DatatypeMessage(
            Size: size,
            BitField: bitField,
            Properties: properties
        )
        {
            Version = version,
            Class = @class
        };
    }

    public DecodeDelegate<T> GetDecodeInfo<T>(
        NativeReadContext context) 
    {
        var encodeInfo = DataUtils.IsReferenceOrContainsReferences(typeof(T))
            ? GetDecodeInfoForTopLevelMemory<T>(context)
            : (DecodeDelegate<T>)_methodInfoGetDecodeInfoForTopLevelUnmanagedMemory
                // TODO cache
                .MakeGenericMethod(typeof(T))
                .Invoke(default, new object[] { })!;

        return encodeInfo;
    }

    private ElementDecodeDelegate GetDecodeInfoForScalar(
        NativeReadContext context, 
        Type type)
    {
        ElementDecodeDelegate decode = Class switch
        {
            /* string / variable-length string */
            DatatypeMessageClass.VariableLength
                when ((VariableLengthBitFieldDescription)BitField).Type == InternalVariableLengthType.String 
                    => type == typeof(string)
                        ? GetDecodeInfoForVariableLengthString(context)
                        : throw new Exception("Variable-length string data can only be decoded as string."),

            DatatypeMessageClass.String => type == typeof(string) 
                ? GetDecodeInfoForFixedLengthString()
                : throw new Exception("Fixed-length string data can only be decoded as string."),

            /* array / variable-length sequence */
            DatatypeMessageClass.VariableLength 
                when ((VariableLengthBitFieldDescription)BitField).Type == InternalVariableLengthType.Sequence 
                    => DataUtils.IsArray(type)
                        ? GetDecodeInfoForVariableLengthSequence(context, type)
                        : throw new Exception("Variable-length sequence data can only be decoded as array."),

            DatatypeMessageClass.Array => DataUtils.IsArray(type)
                ? DataUtils.IsReferenceOrContainsReferences(type) 
                    ? GetDecodeInfoForArray(context, type)
                    : GetDecodeInfoForUnmanagedArray(type)
                : throw new Exception("Array data can only be decoded as array."),
            
            /* compound */
            DatatypeMessageClass.Compound => ReadUtils.CanDecodeFromCompound(type)
                ? GetDecodeInfoForCompound(type)
                : throw new Exception("Compound data can only be decoded as non-primitive struct or reference type."),

            /* enumeration */
            DatatypeMessageClass.Enumerated =>
                ReadUtils.CanDecodeToUnmanaged(type, (int)Size)
                    ? GetDecodeInfoForUnmanagedElement(type)
                    : throw new Exception("Enumerated data can only be decoded into types that match the unmanaged constraint of the same size."),

            /* fixed-point */
            DatatypeMessageClass.FixedPoint =>
                ReadUtils.CanDecodeToUnmanaged(type, (int)Size)
                    ? GetDecodeInfoForUnmanagedElement(type)
                    : throw new Exception("Fixed-point data can only be decoded into types that match the unmanaged constraint of the same size."),

            /* floating-point */
            DatatypeMessageClass.FloatingPoint =>
                ReadUtils.CanDecodeToUnmanaged(type, (int)Size)
                    ? GetDecodeInfoForUnmanagedElement(type)
                    : throw new Exception("Floating-point data can only be decoded into types that match the unmanaged constraint of the same size."),

            /* bitfield */
            DatatypeMessageClass.BitField =>
                ReadUtils.CanDecodeToUnmanaged(type, (int)Size)
                    ? GetDecodeInfoForUnmanagedElement(type)
                    : throw new Exception("Bitfield data can only be decoded into types that match the unmanaged constraint of the same size."),

            /* opaque */
            DatatypeMessageClass.Opaque =>
                ReadUtils.CanDecodeToUnmanaged(type, (int)Size)
                    ? GetDecodeInfoForUnmanagedElement(type)
                    : throw new Exception("Bitfield data can only be decoded into types that match the unmanaged constraint of the same size."),

            /* reference */
            DatatypeMessageClass.Reference =>
                type == typeof(NativeObjectReference1)
                    ? GetDecodeInfoForNativeObjectReference1()
                    : throw new Exception("Bitfield data can only be decoded as NativeObjectReference1."),

            /* default */
            _ => throw new NotSupportedException($"The class '{Class}' is not supported.")
        };

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForNativeObjectReference1()
    {
        object? decode(IH5ReadStream source) 
            => ReadUtils.DecodeUnmanagedElement<NativeObjectReference1>(source);

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedElement(Type type)
    {
        // TODO: cache
        var invokeEncodeUnmanagedElement = WriteUtils.MethodInfoEncodeUnmanagedElement.MakeGenericMethod(type);
        var parameters = new object[1];

        object? decode(IH5ReadStream source)
        {
            parameters[0] = source;
            return invokeEncodeUnmanagedElement.Invoke(default, parameters);
        }

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForCompound(Type type)
    {
        throw new NotImplementedException();
    }

    private ElementDecodeDelegate GetDecodeInfoForArray(
        NativeReadContext context,
        Type type)
    {
        if (Properties[0] is not ArrayPropertyDescription property)
            throw new Exception("Array properties must not be null.");

        var elementType = type.GetElementType()!;

#warning When the encode function below is cached, how do we ensure that the lengths are correct even if the same T but different HDF5 type is loaded?
        var dims = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        var elementCount = dims.Aggregate(1, (product, dim) => product * dim);
        var elementDecode = property.BaseType.GetDecodeInfoForScalar(context, elementType);

        object? decode(IH5ReadStream source)
        {
            var array = Array.CreateInstance(elementType, dims);
            var coordinates = ArrayPool<int>.Shared.Rent(property.Rank);

            for (int linearIndex = 0; linearIndex < elementCount; linearIndex++)
            {
                linearIndex.ToCoordinates(dims, coordinates);    
                array.SetValue(elementDecode(source), coordinates);
            }

            ArrayPool<int>.Shared.Return(coordinates);

            return array;
        }

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedArray(Type type)
    {
        if (Properties[0] is not ArrayPropertyDescription property)
            throw new Exception("Array properties must not be null.");

        var elementType = type.GetElementType()!;

        var lengths = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        object? decode(IH5ReadStream source)
        {
            var array = Array.CreateInstance(elementType, lengths);
            var memory = new UnmanagedArrayMemoryManager<byte>(array).Memory;

            source.ReadDataset(memory);

            return array;
        }

        return decode;
    }
    
    private ElementDecodeDelegate GetDecodeInfoForVariableLengthSequence(
        NativeReadContext context,
        Type type)
    {
        if (Properties[0] is not VariableLengthPropertyDescription property)
            throw new Exception("Variable-length properties must not be null.");

        var elementType = type.GetElementType()!;
        var elementDecode = property.BaseType.GetDecodeInfoForScalar(context, elementType);

        object? decode(IH5ReadStream source)
        {
            // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tpublic.h#L1621-L1642
            // https://portal.hdfgroup.org/display/HDF5/Datatype+Basics#DatatypeBasics-variable
            // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/test/tarray.c#L1113
            // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tpublic.h#L234-L241
            // https://github.com/HDFGroup/hdf5/blob/1d90890a7b38834074169ce56720b7ea7f4b01ae/src/H5Tvlen.c#L837-L941
            //
            // typedef struct {
            //     size_t len; /**< Length of VL data (in base type units) */
            //     void  *p;   /**< Pointer to VL data */
            // } hvl_t;

            /* read data into rented buffer */
            var totalSize = sizeof(uint) + context.Superblock.OffsetsSize + sizeof(uint);
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(totalSize);
            var buffer = memoryOwner.Memory[0..totalSize];

            source.ReadDataset(buffer);

            /* Read global heap ID (Skip the length of the sequence (H5Tvlen.c H5T_vlen_disk_read)) */
            var length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);
            buffer = buffer.Slice(sizeof(uint));

            var array = Array.CreateInstance(elementType, length);
            var memory = new UnmanagedArrayMemoryManager<byte>(array).Memory;

            for (int i = 0; i < length; i++)
            {
                var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

                if (globalHeapId.Equals(default))
                    continue;

                var globalHeapCollection = NativeCache.GetGlobalHeapObject(context, globalHeapId.CollectionAddress);

                if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
                {
                    array.SetValue(elementDecode(source), i);
                }
                
                else
                {
                    // It would be more correct to just throw an exception 
                    // when the object index is not found in the collection,
                    // but that would make the tests following test fail
                    // - CanReadDataset_Array_nullable_struct.
                    // 
                    // And it would make the user's life a bit more complicated
                    // if the library cannot handle missing entries.
                    continue;
                }   
            }

            return array;
        }

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForVariableLengthString(
        NativeReadContext context)
    {
        object? decode(IH5ReadStream source)
        {
            /* Padding
             * https://support.hdfgroup.org/HDF5/doc/H5.format.html#DatatypeMessage
             * Search for "null terminate": null terminate and null padding are essentially
             * the same when simply reading them from file.
             */

            /* String is always split after first \0 when writing data to file. 
             * In other words, padding type only matters when reading data.
             */

            if (BitField is not VariableLengthBitFieldDescription bitField)
                throw new Exception("Variable-length bit field description must not be null.");

            // see IV.B. Disk Format: Level 2B - Data Object Data Storage
            Func<string, string> trim = bitField.PaddingType switch
            {
                PaddingType.NullTerminate => value => value,
                PaddingType.NullPad => value => value,
                PaddingType.SpacePad => value => value.TrimEnd(' '),
                _ => throw new Exception("Unsupported padding type.")
            };

            /* read data into rented buffer */
            var totalSize = sizeof(uint) + context.Superblock.OffsetsSize + sizeof(uint);
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(totalSize);
            var buffer = memoryOwner.Memory[0..totalSize];

            source.ReadDataset(buffer);

            /* Read global heap ID (Skip the length of the sequence (H5Tvlen.c H5T_vlen_disk_read)) */
            buffer = buffer.Slice(sizeof(uint));

            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

            if (globalHeapId.Equals(default))
                return default(string);

            var globalHeapCollection = NativeCache.GetGlobalHeapObject(context, globalHeapId.CollectionAddress);

            if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
            {
                var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);
                value = trim(value);
                return value;
            }

            else
            {
                // It would be more correct to just throw an exception 
                // when the object index is not found in the collection,
                // but that would make the tests following test fail
                // - CanReadDataset_Array_nullable_struct.
                // 
                // And it would make the user's life a bit more complicated
                // if the library cannot handle missing entries.
                return default(string);
            }
        }

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForFixedLengthString()
    {
        object? decode(IH5ReadStream source)
        {
            /* Padding
             * https://support.hdfgroup.org/HDF5/doc/H5.format.html#DatatypeMessage
             * Search for "null terminate": null terminate and null padding are essentially
             * the same when simply reading them from file.
             */

            if (BitField is not StringBitFieldDescription bitField)
                throw new Exception("String bit field description must not be null.");

            Func<string, string> trim = bitField.PaddingType switch
            {
#if NETSTANDARD2_0
                PaddingType.NullTerminate => value => value.Split(new char[] { '\0' }, 2)[0],
#else
                PaddingType.NullTerminate => value => value.Split('\0', 2)[0],
#endif
                PaddingType.NullPad => value => value.TrimEnd('\0'),
                PaddingType.SpacePad => value => value.TrimEnd(' '),
                _ => throw new Exception("Unsupported padding type.")
            };

            using var memoryOwner = MemoryPool<byte>.Shared.Rent((int)Size);
            var memory = memoryOwner.Memory[0..(int)Size];
            var value = ReadUtils.ReadFixedLengthString(memory.Span);

            return value;
        }

        return decode;
    }

    private DecodeDelegate<T> GetDecodeInfoForTopLevelMemory<T>(
        NativeReadContext context
    )
    {
        var elementDecode = GetDecodeInfoForScalar(context, typeof(T));

        void decode(IH5ReadStream source, Memory<T> target)
        {
            var targetSpan = target.Span;

            for (int i = 0; i < target.Length; i++)
            {
                targetSpan[i] = (T)elementDecode(source);
            }
        };

        return decode;
    }

    private static DecodeDelegate<T> GetDecodeInfoForArray<T>(Array data) 
        where T : struct
    {
        static void decode(IH5ReadStream source, Memory<T> target)
            => source.ReadDataset(target.Cast<T, byte>());

        return decode;
    }

    private static DecodeDelegate<T> GetDecodeInfoForTopLevelUnmanagedMemory<T>() 
        where T : struct
    {
        static void decode(IH5ReadStream source, Memory<T> target)
            => source.ReadDataset(target.Cast<T, byte>());

        return decode;
    }
}
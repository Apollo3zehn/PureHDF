using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class DatatypeMessage(
    uint Size,
    DatatypeBitFieldDescription BitField,
    DatatypePropertyDescription[] Properties
) : Message
{
    private static readonly MethodInfo _methodInfoGetDecodeInfoForUnmanagedMemory = typeof(DatatypeMessage)
        .GetMethod(nameof(GetDecodeInfoForUnmanagedMemory), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    public bool IsReferenceOrContainsReferences()
    {
        return Class switch
        {
            DatatypeMessageClass.FixedPoint => false,
            DatatypeMessageClass.FloatingPoint => false,
            DatatypeMessageClass.String => false,
            DatatypeMessageClass.BitField => false,
            DatatypeMessageClass.Opaque => false,
            DatatypeMessageClass.Compound => Properties
                .Cast<CompoundPropertyDescription>()
                .Any(description => description.MemberTypeMessage.IsReferenceOrContainsReferences()),
            DatatypeMessageClass.Reference => false,
            DatatypeMessageClass.Enumerated => false,
            DatatypeMessageClass.VariableLength => true,
            DatatypeMessageClass.Array => ((ArrayPropertyDescription)Properties[0]).BaseType.IsReferenceOrContainsReferences(),
            _ => throw new NotSupportedException($"The data type message class '{Class}' is not supported.")
        };
    }

    public DecodeDelegate<T> GetDecodeInfo<T>(
        NativeReadContext context) 
    {
        var memoryIsRef = DataUtils.IsReferenceOrContainsReferences(typeof(T));
        var fileIsRef = IsReferenceOrContainsReferences();

        var memoryTypeSize = memoryIsRef
            ? default
            : Unsafe.SizeOf<T>();

        var fileTypeSize = Size;

        // according to type-mismatch-behavior.md
        // TODO cache
        return (memoryIsRef, fileIsRef) switch
        {
            (true, _) => GetDecodeInfoForReferenceMemory<T>(context),
            (false, true) => throw new Exception("Unable to decode a reference type as value type."),
            (false, false) when memoryTypeSize == fileTypeSize => (DecodeDelegate<T>)_methodInfoGetDecodeInfoForUnmanagedMemory
                .MakeGenericMethod(typeof(T))
                .Invoke(default, new object[] { })!,
            _ => throw new Exception("Unable to decode values types of different type size.")
        };
    }

    private (Type Type, ElementDecodeDelegate Decode) GetDecodeInfoForScalar(
        NativeReadContext context, 
        Type? memoryType)
    {
        return Class switch
        {
            /* string / variable-length string */
            DatatypeMessageClass.String => 
                memoryType is null || memoryType == typeof(string) 
                    ? (typeof(string), GetDecodeInfoForFixedLengthString())
                    : throw new Exception($"Fixed-length string data can only be decoded as string (incompatible type: {memoryType})."),

            DatatypeMessageClass.VariableLength when ((VariableLengthBitFieldDescription)BitField).Type == InternalVariableLengthType.String =>
                memoryType is null || memoryType == typeof(string)
                    ? (typeof(string), GetDecodeInfoForVariableLengthString(context))
                    : throw new Exception($"Variable-length string data can only be decoded as string (incompatible type: {memoryType})."),

            /* array / variable-length sequence */
            DatatypeMessageClass.Array => 
                memoryType is null || DataUtils.IsArray(memoryType)
                    ? GetDecodeInfoForArray(context, memoryType)
                    : throw new Exception($"Array data can only be decoded as array (incompatible type: {memoryType})."),

            DatatypeMessageClass.VariableLength when ((VariableLengthBitFieldDescription)BitField).Type == InternalVariableLengthType.Sequence =>
                memoryType is null || DataUtils.IsArray(memoryType)
                    ? GetDecodeInfoForVariableLengthSequence(context, memoryType)
                    : throw new Exception($"Variable-length sequence data can only be decoded as array (incompatible type: {memoryType})."),
            
            /* compound */
            DatatypeMessageClass.Compound => 
                memoryType is null || ReadUtils.CanDecodeFromCompound(memoryType)
                    ? GetDecodeInfoForCompound(context, memoryType /* isObject = true is OK here */)
                    : throw new Exception($"Compound data can only be decoded as non-primitive struct or reference type (incompatible type: {memoryType})."),

            /* enumeration */
            DatatypeMessageClass.Enumerated =>
                memoryType is null || ReadUtils.CanDecodeToUnmanaged(memoryType, (int)Size)
                    ? memoryType is null
                        ? ((EnumerationPropertyDescription)Properties[0]).BaseType.GetDecodeInfoForScalar(context, memoryType: default)
                        : (memoryType, GetDecodeInfoForUnmanagedElement(memoryType))
                    : throw new Exception($"Enumerated data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* fixed-point */
            DatatypeMessageClass.FixedPoint =>
                memoryType is null || ReadUtils.CanDecodeToUnmanaged(memoryType, (int)Size)
                    ? memoryType is null
                        ? (Size, ((FixedPointBitFieldDescription)BitField).IsSigned) switch
                            {
                                (1, false) => (typeof(byte), GetDecodeInfoForUnmanagedElement<byte>()),
                                (1, true) => (typeof(sbyte), GetDecodeInfoForUnmanagedElement<sbyte>()),
                                (2, false) => (typeof(ushort), GetDecodeInfoForUnmanagedElement<ushort>()),
                                (2, true) => (typeof(short), GetDecodeInfoForUnmanagedElement<short>()),
                                (4, false) => (typeof(uint), GetDecodeInfoForUnmanagedElement<uint>()),
                                (4, true) => (typeof(int), GetDecodeInfoForUnmanagedElement<int>()),
                                (8, false) => (typeof(ulong), GetDecodeInfoForUnmanagedElement<ulong>()),
                                (8, true) => (typeof(long), GetDecodeInfoForUnmanagedElement<long>()),
    #if NET7_0_OR_GREATER
                                (16, false) => (typeof(UInt128), GetDecodeInfoForUnmanagedElement<UInt128>()),
                                (16, true) => (typeof(Int128), GetDecodeInfoForUnmanagedElement<Int128>()),
    #endif
                                _ => throw new Exception("Unable to decode fixed-point data without additional runtime type information.")
                            }
                        : (memoryType, GetDecodeInfoForUnmanagedElement(memoryType))
                    : throw new Exception($"Fixed-point data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* floating-point */
            DatatypeMessageClass.FloatingPoint =>
                memoryType is null || ReadUtils.CanDecodeToUnmanaged(memoryType, (int)Size)
                    ? memoryType is null
                        ? Size switch 
                            {
#if NET5_0_OR_GREATER
                                2 => (typeof(Half), GetDecodeInfoForUnmanagedElement<Half>()),
#endif
                                4 => (typeof(float), GetDecodeInfoForUnmanagedElement<float>()),
                                8 => (typeof(double), GetDecodeInfoForUnmanagedElement<double>()),
                                _ => throw new Exception("Unable to decode floating-point data without additional runtime type information.")
                            }
                        : (memoryType, GetDecodeInfoForUnmanagedElement(memoryType))
                    : throw new Exception($"Floating-point data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* bitfield */
            DatatypeMessageClass.BitField =>
                memoryType is null || ReadUtils.CanDecodeToUnmanaged(memoryType, (int)Size)
                    ? memoryType is null
                        ? Size switch
                            {
                                1 => (typeof(byte), GetDecodeInfoForUnmanagedElement<byte>()),
                                2 => (typeof(ushort), GetDecodeInfoForUnmanagedElement<ushort>()),
                                4 => (typeof(uint), GetDecodeInfoForUnmanagedElement<uint>()),
                                8 => (typeof(ulong), GetDecodeInfoForUnmanagedElement<ulong>()),
#if NET7_0_OR_GREATER
                                16 => (typeof(UInt128), GetDecodeInfoForUnmanagedElement<UInt128>()),
#endif
                                _ => throw new Exception("Unable to decode bitfield data without additional runtime type information.")
                            }
                        : (memoryType, GetDecodeInfoForUnmanagedElement(memoryType))
                    : throw new Exception($"Bitfield data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* opaque */
            DatatypeMessageClass.Opaque =>
                memoryType is null || ReadUtils.CanDecodeToUnmanaged(memoryType, (int)Size)
                    ? memoryType is null
                        ? (typeof(byte[]), GetDecodeInfoForOpaqueAsByteArray())
                        : (memoryType, GetDecodeInfoForUnmanagedElement(memoryType))
                    : throw new Exception($"Bitfield data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* reference */
            DatatypeMessageClass.Reference =>
                memoryType is null || memoryType == typeof(NativeObjectReference1)
                    ? (typeof(NativeObjectReference1), GetDecodeInfoForUnmanagedElement<NativeObjectReference1>())
                    : throw new Exception($"Bitfield data can only be decoded as NativeObjectReference1 (incompatible type: {memoryType})."),

            /* default */
            _ => throw new NotSupportedException($"The class '{Class}' is not supported.")
        };
    }

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedElement<T>() where T : struct
    {
        object? decode(IH5ReadStream source) 
            => ReadUtils.DecodeUnmanagedElement<T>(source);

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedElement(Type type)
    {
        // TODO: cache
        var invokeEncodeUnmanagedElement = ReadUtils.MethodInfoDecodeUnmanagedElement.MakeGenericMethod(type);
        var parameters = new object[1];

        object? decode(IH5ReadStream source)
        {
            parameters[0] = source;
            return invokeEncodeUnmanagedElement.Invoke(default, parameters);
        }

        return decode;
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForCompound(
        NativeReadContext context,
        Type? memoryType)
    {
        /* read unknown compound */
        if (memoryType is null || memoryType == typeof(Dictionary<string, object?>))
        {
            var decodeSteps = Properties
                .Cast<CompoundPropertyDescription>()
                .Select(property => (property, property.MemberTypeMessage.GetDecodeInfoForScalar(context, memoryType: default).Decode))
                .ToArray();

            object? decode(IH5ReadStream source)
            {
                var result = new Dictionary<string, object?>();
                var basePosition = source.Position;

                foreach (var decodeStep in decodeSteps)
                {
                    var (property, decoder) = decodeStep;

                    // skip padding
                    var consumed = source.Position - basePosition;
                    var padding = (long)property.MemberByteOffset - consumed;

                    if (padding < 0)
                        throw new Exception("This should never happen.");

                    if (padding > 0)
                        source.Seek(padding, SeekOrigin.Current);

                    // decode
                    result[property.Name] = decoder(source);
                }

                // skip padding
                var totalConsumed = source.Position - basePosition;
                var totalPadding = Size - totalConsumed;

                if (totalPadding < 0)
                    throw new Exception("This should never happen.");

                if (totalPadding > 0)
                    source.Seek(totalPadding, SeekOrigin.Current);

                return result;
            }

            return (typeof(Dictionary<string, object?>), decode);
        }

        /* read known compound */
        else
        {
            var memoryIsRef = DataUtils.IsReferenceOrContainsReferences(memoryType);
            var fileIsRef = IsReferenceOrContainsReferences();

            var memoryTypeSize = memoryIsRef
                ? default
                : DataUtils.UnmanagedSizeOf(memoryType);

            var fileTypeSize = Size;

            // according to type-mismatch-behavior.md
            // TODO cache
            var decode = (memoryIsRef, fileIsRef) switch
            {
                (true, _) => GetDecodeInfoForReferenceCompound(context, memoryType),
                (false, true) => throw new Exception("Unable to decode a reference type as value type."),
                (false, false) when memoryTypeSize == fileTypeSize => GetDecodeInfoForUnmanagedElement(memoryType),
                _ => throw new Exception("Unable to decode values types of different type size.")
            };

            return (memoryType, decode);
        }
    }

    private ElementDecodeDelegate GetDecodeInfoForReferenceCompound(
        NativeReadContext context,
        Type type)
    {
        var isValueType = type.IsValueType;

        if (!isValueType && type.GetConstructor(Type.EmptyTypes) is null)
            throw new Exception("Only types with parameterless constructors are supported to decode compound data.");

        var compoundProperties = Properties.Cast<CompoundPropertyDescription>().ToArray();
        var decodeSteps = new DecodeStep[compoundProperties.Length];

        // fields
        var includeFields = isValueType
            ? context.ReadOptions.IncludeStructFields
            : context.ReadOptions.IncludeClassFields;

        var fieldInfos = includeFields
            ? type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            : Array.Empty<FieldInfo>();

        // properties
        var includeProperties = isValueType
            ? context.ReadOptions.IncludeStructProperties
            : context.ReadOptions.IncludeClassProperties;

        var propertyInfos = includeProperties
            ? type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(propertyInfo => propertyInfo.CanRead)
                .ToArray()
            : Array.Empty<PropertyInfo>();

        if (includeFields)
        {
            var fieldNameMapper = context.ReadOptions.FieldNameMapper;

            var fieldNameToInfoMap = fieldInfos.ToDictionary(
                fieldInfo => fieldNameMapper is null ? fieldInfo.Name : fieldNameMapper(fieldInfo) ?? fieldInfo.Name,
                fieldInfo => fieldInfo
            );

            for (int i = 0; i < compoundProperties.Length; i++)
            {
                var compoundProp = compoundProperties[i];

                if (fieldNameToInfoMap.TryGetValue(compoundProp.Name, out var fieldInfo))
                {
                    var elementDecode = compoundProp.MemberTypeMessage
                        .GetDecodeInfoForScalar(context, fieldInfo.FieldType).Decode;

                    decodeSteps[i] = new DecodeStep(
                        SetValue: fieldInfo.SetValue,
                        CompoundMemberOffset: compoundProp.MemberByteOffset,
                        ElementDecode: elementDecode
                    );
                }
            }
        }

        if (includeProperties)
        {
            var propertyNameMapper = context.ReadOptions.PropertyNameMapper;

            var propertyNameToInfoMap = propertyInfos.ToDictionary(
                propertyInfo => propertyNameMapper is null ? propertyInfo.Name : propertyNameMapper(propertyInfo) ?? propertyInfo.Name,
                propertyInfo => propertyInfo
            );

            for (int i = 0; i < compoundProperties.Length; i++)
            {
                if (!decodeSteps[i].Equals(default))
                    continue;

                var compoundProp = compoundProperties[i];

                if (propertyNameToInfoMap.TryGetValue(compoundProp.Name, out var propertyInfo))
                {
                    var elementDecode = compoundProp.MemberTypeMessage
                        .GetDecodeInfoForScalar(context, propertyInfo.PropertyType).Decode;

                    decodeSteps[i] = new DecodeStep(
                        SetValue: propertyInfo.SetValue,
                        CompoundMemberOffset: compoundProp.MemberByteOffset,
                        ElementDecode: elementDecode
                    );
                }
            }
        }

        // look for not mapped compound properties
        var previousOffset = 0UL;

        for (int i = 0; i < decodeSteps.Length; i++)
        {
            if (!decodeSteps[i].Equals(default))
                continue;

            var compoundProp = compoundProperties[i];

            ElementDecodeDelegate elementDecode;

            elementDecode = (IH5ReadStream source) => 
            {
                var nextOffset = i == compoundProperties.Length - 1
                    ? Size
                    : compoundProp.MemberByteOffset;

                var offset = nextOffset - compoundProp.MemberByteOffset;

                source.Seek((long)offset, SeekOrigin.Current);
                return default;
            };

            decodeSteps[i] = new DecodeStep(
                SetValue: default,
                CompoundMemberOffset: compoundProp.MemberByteOffset,
                ElementDecode: elementDecode
            );

            previousOffset = compoundProp.MemberByteOffset;
        }

#warning Cache only static decode methods! Other methods may depend on HDF5 type specifics
        // decode
        object? decode(IH5ReadStream source)
        {
            var result = Activator.CreateInstance(type)!;
            var basePosition = source.Position;

            foreach (var decodeStep in decodeSteps)
            {
                var (setValue, offset, decoder) = decodeStep;

                // skip padding
                var consumed = source.Position - basePosition;
                var padding = (long)offset - consumed;

                if (padding < 0)
                    throw new Exception("This should never happen.");

                if (padding > 0)
                    source.Seek(padding, SeekOrigin.Current);

                // decode
                setValue?.Invoke(result, decoder(source));
            }

            // skip padding
            var totalConsumed = source.Position - basePosition;
            var totalPadding = Size - totalConsumed;

            if (totalPadding < 0)
                throw new Exception("This should never happen.");

            if (totalPadding > 0)
                source.Seek(totalPadding, SeekOrigin.Current);

            return result;
        }

        return decode;
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForArray(
        NativeReadContext context,
        Type? memoryType)
    {
        if (Properties[0] is not ArrayPropertyDescription property)
            throw new Exception("Variable-length properties must not be null.");

        var elementType = memoryType?.GetElementType();
        (elementType, var elementDecode) = property.BaseType.GetDecodeInfoForScalar(context, elementType);

        var memoryIsRef = DataUtils.IsReferenceOrContainsReferences(elementType);
        var fileIsRef = IsReferenceOrContainsReferences();

        var memoryTypeSize = memoryIsRef
            ? default
            : DataUtils.UnmanagedSizeOf(elementType);

        var fileTypeSize = ((ArrayPropertyDescription)Properties[0]).BaseType.Size;

        // according to type-mismatch-behavior.md
        // TODO cache
        var decode = (memoryIsRef, fileIsRef) switch
        {
            (true, _) => GetDecodeInfoForReferenceArray(elementType, elementDecode, property),
            (false, true) => throw new Exception("Unable to decode a reference type as value type."),
            (false, false) when memoryTypeSize == fileTypeSize => GetDecodeInfoForUnmanagedArray(elementType, property),
            _ => throw new Exception("Unable to decode values types of different type size.")
        };

        memoryType ??= Type.GetType($"{elementType}[{new string(',', property.Rank)}]") 
            ?? throw new Exception($"Unable to find array type for element type {elementType}.");

        return (memoryType, decode);
    }

    private static ElementDecodeDelegate GetDecodeInfoForReferenceArray(
        Type elementType,
        ElementDecodeDelegate elementDecode,
        ArrayPropertyDescription property)
    {
#warning Cache only static decode methods! Other methods may depend on HDF5 type specifics
        var dims = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        var elementCount = dims.Aggregate(1, (product, dim) => product * dim);

        // TODO: cache
        var invokeDecodeArray = ReadUtils.MethodInfoDecodeReferenceArray.MakeGenericMethod(elementType);
        var parameters = new object[3];

        object? decode(IH5ReadStream source)
        {
            parameters[0] = source;
            parameters[1] = dims;
            parameters[2] = elementDecode;

            return invokeDecodeArray.Invoke(default, parameters);
        }

        return decode;
    }

    private static ElementDecodeDelegate GetDecodeInfoForUnmanagedArray(
        Type elementType,
        ArrayPropertyDescription property
    )
    {
#warning Cache only static decode methods! Other methods may depend on HDF5 type specifics
        var dims = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        // TODO: cache
        var invokeDecodeUnmanagedArray = ReadUtils.MethodInfoDecodeUnmanagedArray.MakeGenericMethod(elementType);
        var parameters = new object[2];

        object? decode(IH5ReadStream source)
        {
            parameters[0] = source;
            parameters[1] = dims;

            return invokeDecodeUnmanagedArray.Invoke(default, parameters);
        }

        return decode;
    }
    
    private ElementDecodeDelegate GetDecodeInfoForOpaqueAsByteArray()
    {
#warning Cache only static decode methods! Other methods may depend on HDF5 type specifics
        var dims = new int[] { (int)Size };

        object? decode(IH5ReadStream source)
        {
            return ReadUtils.DecodeUnmanagedArray<byte>(source, dims);
        }

        return decode;
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForVariableLengthSequence(
        NativeReadContext context,
        Type? memoryType)
    {
        if (Properties[0] is not VariableLengthPropertyDescription property)
            throw new Exception("Variable-length properties must not be null.");

        var elementType = memoryType?.GetElementType();
        (elementType, var elementDecode) = property.BaseType.GetDecodeInfoForScalar(context, elementType);

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
            var lengthSize = sizeof(uint);
            var globalHeapIdSize = context.Superblock.OffsetsSize + sizeof(uint);
            var totalSize = lengthSize + globalHeapIdSize;
            
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(totalSize);
            var buffer = memoryOwner.Memory[0..totalSize];

            source.ReadDataset(buffer);

            /* decode sequence length */
            var sequenceLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);
            buffer = buffer.Slice(lengthSize);

            /* decode global heap IDs and get associated data */
            var array = Array.CreateInstance((Type?)elementType, sequenceLength);
            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

            if (globalHeapId.Equals(default))
                return default;

            buffer = buffer.Slice(globalHeapIdSize);

            var globalHeapCollection = NativeCache.GetGlobalHeapObject(
                context, 
                globalHeapId.CollectionAddress,
                restoreAddress: true);

            if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
            {
                // TODO: cache short-lived stream?
                var localSource = new SystemMemoryStream(globalHeapObject.ObjectData);

                for (int i = 0; i < sequenceLength; i++)
                {
                    array.SetValue(elementDecode(localSource), i);
                }

                return array;
            }
            
            else
            {
                // It would be more correct to just throw an exception 
                // when the object index is not found in the collection,
                // but that would make the tests following test fail
                // - CanRead_Array_nullable_struct.
                // 
                // And it would make the user's life a bit more complicated
                // if the library cannot handle missing entries.
                return default;
            }
        }

        memoryType ??= Type.GetType($"{elementType}[]") 
            ?? throw new Exception($"Unable to find array type for element type {elementType}.");

        return (memoryType, decode);
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

            /* skip the length of the sequence (H5Tvlen.c H5T_vlen_disk_read) */
            buffer = buffer.Slice(sizeof(uint));

            /* decode global heap IDs and get associated data */
            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

            if (globalHeapId.Equals(default))
                return default;

            var globalHeapCollection = NativeCache.GetGlobalHeapObject(
                context, 
                globalHeapId.CollectionAddress,
                restoreAddress: true);

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
                // - CanRead_Array_nullable_struct.
                // 
                // And it would make the user's life a bit more complicated
                // if the library cannot handle missing entries.
                return default;
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

            source.ReadDataset(memory);

            var value = ReadUtils.ReadFixedLengthString(memory.Span);
            value = trim(value);

            return value;
        }

        return decode;
    }

    private DecodeDelegate<T> GetDecodeInfoForReferenceMemory<T>(
        NativeReadContext context
    )
    {
        var elementDecode = GetDecodeInfoForScalar(context, typeof(T)).Decode;

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

    private static DecodeDelegate<T> GetDecodeInfoForUnmanagedMemory<T>() 
        where T : struct
    {
        static void decode(IH5ReadStream source, Memory<T> target)
            => source.ReadDataset(target.Cast<T, byte>());

        return decode;
    }
}
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq.Expressions;
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

    private static readonly MethodInfo _methodInfoBuildVariableLengthSequenceUnmanagedDecoder = typeof(DatatypeMessage)
        .GetMethod(nameof(BuildVariableLengthSequenceUnmanagedDecoder), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    public static async ValueTask<DatatypeMessage> Decode(H5DriverBase driver)
    {
        var classVersion = await driver.ReadByte().ConfigureAwait(false);
        var version = (byte)(classVersion >> 4);
        var @class = (DatatypeMessageClass)(classVersion & 0x0F);

        DatatypeBitFieldDescription bitField = @class switch
        {
            DatatypeMessageClass.FixedPoint => await FixedPointBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.FloatingPoint => await FloatingPointBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Time => await TimeBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.String => await StringBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.BitField => await BitFieldBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Opaque => await OpaqueBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Compound => await CompoundBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Reference => await ReferenceBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Enumerated => await EnumerationBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.VariableLength => await VariableLengthBitFieldDescription.Decode(driver).ConfigureAwait(false),
            DatatypeMessageClass.Array => await ArrayBitFieldDescription.Decode(driver).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The data type message class '{@class}' is not supported.")
        };

        var size = await driver.ReadUInt32().ConfigureAwait(false);

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
                DatatypeMessageClass.FixedPoint => await FixedPointPropertyDescription.Decode(driver).ConfigureAwait(false),
                DatatypeMessageClass.FloatingPoint => await FloatingPointPropertyDescription.Decode(driver).ConfigureAwait(false),
                DatatypeMessageClass.Time => await TimePropertyDescription.Decode(driver).ConfigureAwait(false),
                DatatypeMessageClass.BitField => await BitFieldPropertyDescription.Decode(driver).ConfigureAwait(false),
                DatatypeMessageClass.Opaque => await OpaquePropertyDescription.Decode(driver, ((OpaqueBitFieldDescription)bitField).TagByteLength).ConfigureAwait(false),
                DatatypeMessageClass.Compound => await CompoundPropertyDescription.Decode(driver, version, size).ConfigureAwait(false),
                DatatypeMessageClass.Enumerated => await EnumerationPropertyDescription.Decode(driver, version, size, ((EnumerationBitFieldDescription)bitField).MemberCount).ConfigureAwait(false),
                DatatypeMessageClass.VariableLength => await VariableLengthPropertyDescription.Decode(driver).ConfigureAwait(false),
                DatatypeMessageClass.Array => await ArrayPropertyDescription.Decode(driver, version).ConfigureAwait(false),
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

    // Caches the DecodeDelegate<TElement> produced for each (TElement, isRawMode)
    // pair on this DatatypeMessage instance, so repeated Read calls on the same
    // dataset reuse one decoder instead of rebuilding the closure tree (and paying
    // its inner MethodInfo.Invoke into GetDecodeInfoForUnmanagedMemory) every time.
    //
    // The cached delegates do NOT capture a NativeReadContext: DecodeDelegate<T> /
    // ElementDecodeDelegate take the context as a call-time parameter instead (see
    // ReadTypes.cs). That is required because a planned follow-up allocates one
    // H5DriverBase (and therefore one NativeReadContext) per read operation, while
    // this cache - keyed on (Type, isRawMode) only, with no context in the key - is
    // shared across every operation on the dataset/attribute that owns this
    // DatatypeMessage. A closure that captured the context from the first build
    // would silently keep reading through the first operation's driver on every
    // later call. Build-time-only use of a context parameter below (e.g. to decide
    // which struct fields/properties to map, from context.ReadOptions) is fine -
    // that produces plain data baked into the decodeSteps once, not a captured
    // context reference - and mirrors the original invariant that ReadOptions is
    // stable for the lifetime of the file, unlike the driver.
    private readonly ConcurrentDictionary<(Type, bool), Delegate> _decodeInfoCache = new();

    public DecodeDelegate<TElement> GetDecodeInfo<TElement>(
        NativeReadContext context,
        bool isRawMode)
    {
        var key = (typeof(TElement), isRawMode);

        if (_decodeInfoCache.TryGetValue(key, out var cached))
            return (DecodeDelegate<TElement>)cached;

        var built = BuildDecodeInfo<TElement>(context, isRawMode);
        return (DecodeDelegate<TElement>)_decodeInfoCache.GetOrAdd(key, built);
    }

    private DecodeDelegate<TElement> BuildDecodeInfo<TElement>(
        NativeReadContext context,
        bool isRawMode)
    {
        var memoryIsRef = DataUtils.IsReferenceOrContainsReferences(typeof(TElement));
        var fileIsRef = IsReferenceOrContainsReferences();

        var memoryTypeSize = memoryIsRef
            ? default
            : Unsafe.SizeOf<TElement>();

        var fileTypeSize = Size;

        // according to type-mismatch-behavior.md
        return (memoryIsRef, fileIsRef) switch
        {
            (true, _) 
                => GetDecodeInfoForReferenceMemory<TElement>(context),

            (false, _) when IsNullableValueTypeAndCanDecode<TElement>() 
                => GetDecodeInfoForReferenceMemory<TElement>(context),

            (false, true) 
                => throw new Exception("Unable to decode a reference type as value type."),

            (false, false) when memoryTypeSize == fileTypeSize || isRawMode
                => (DecodeDelegate<TElement>)_methodInfoGetDecodeInfoForUnmanagedMemory
                    .MakeGenericMethod(typeof(TElement))
                    .Invoke(default, [])!,
            _ 
                => throw new Exception("Unable to decode values types of different type size.")
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
                    ? (typeof(string), GetDecodeInfoForVariableLengthString())
                    : throw new Exception($"Variable-length string data can only be decoded as string (incompatible type: {memoryType})."),

            /* array / nullable value type / variable-length sequence */
            DatatypeMessageClass.Array =>
                memoryType is null || DataUtils.IsArray(memoryType)
                    ? GetDecodeInfoForArray(context, memoryType)
                    : throw new Exception($"Array data can only be decoded as array (incompatible type: {memoryType})."),

            DatatypeMessageClass.VariableLength when ((VariableLengthBitFieldDescription)BitField).Type == InternalVariableLengthType.Sequence =>

                memoryType is null || DataUtils.IsArray(memoryType)

                    ? GetDecodeInfoForVariableLengthSequence(context, memoryType)

                    : Nullable.GetUnderlyingType(memoryType) is null
                        ? throw new Exception($"Variable-length sequence data can only be decoded as array (incompatible type: {memoryType}).")
                        : GetDecodeInfoForNullableValueType(context, memoryType),

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
#if NET8_0_OR_GREATER
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
                            2 => (typeof(Half), GetDecodeInfoForUnmanagedElement<Half>()),
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
#if NET8_0_OR_GREATER
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
                    : throw new Exception($"Opaque data can only be decoded into types that match the struct constraint of the same size (incompatible type: {memoryType})."),

            /* reference */
            DatatypeMessageClass.Reference =>
                memoryType is null || memoryType == typeof(NativeObjectReference1)
                    ? (typeof(NativeObjectReference1), GetDecodeInfoForUnmanagedElement<NativeObjectReference1>())
                    : throw new Exception($"Reference data can only be decoded as NativeObjectReference1 (incompatible type: {memoryType})."),

            /* default */
            _ => throw new NotSupportedException($"The class '{Class}' is not supported.")
        };
    }

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedElement<T>() where T : struct
    {
        // FAST PATH (#162-ish): plain unmanaged element decode. Captures no context, same as
        // GetDecodeInfoForUnmanagedMemory<T> below - the context parameter is unused here, it
        // only exists to match ElementDecodeDelegate's shape.
        // Without the await this returns a boxed ValueTask<T> as the element value.
        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
            => await ReadUtils.DecodeUnmanagedElement<T>(source).ConfigureAwait(false);

        return decode;
    }

    // Builds and caches one ElementDecodeDelegate per element Type. Previously the
    // closure performed MethodInfo.Invoke on every element, which allocated a boxed
    // argument array per call and dominated CPU on element-heavy reads. Routing
    // through a typed delegate built once per element type pays the reflection
    // once at cache-miss time and makes the per-element call a direct invocation
    // of the generic GetDecodeInfoForUnmanagedElement<T>.
    private static readonly ConcurrentDictionary<Type, ElementDecodeDelegate> _unmanagedElementDecoderCache = new();

    // Caches a compiled parameterless-constructor factory per Type. The compound
    // decode closure (GetDecodeInfoForReferenceCompound) invokes the factory on
    // every element of class-typed compound data; a compiled delegate avoids the
    // per-call reflection cost of constructor lookup and invocation. The factory
    // is built once per Type via a compiled expression tree and is shared across
    // all DatatypeMessage instances since the Type recurs across files.
    // GetDecodeInfoForReferenceCompound validates the parameterless ctor for class
    // types before the closure is built, so cache misses never throw.
    private static readonly ConcurrentDictionary<Type, Func<object>> _parameterlessCtorCache = new();

    private static Func<object> GetParameterlessConstructor(Type type)
    {
        return _parameterlessCtorCache.GetOrAdd(type, static t =>
            Expression.Lambda<Func<object>>(Expression.New(t)).Compile());
    }

    private static readonly MethodInfo _methodInfoGetDecodeInfoForUnmanagedElement = typeof(DatatypeMessage)
        .GetMethod(
            nameof(GetDecodeInfoForUnmanagedElement),
            genericParameterCount: 1,
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)!;

    private ElementDecodeDelegate GetDecodeInfoForUnmanagedElement(Type type)
    {
        return _unmanagedElementDecoderCache.GetOrAdd(type, t =>
        {
            var method = _methodInfoGetDecodeInfoForUnmanagedElement.MakeGenericMethod(t);
            return (ElementDecodeDelegate)method.Invoke(this, parameters: null)!;
        });
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForCompound(
        NativeReadContext context,
        Type? memoryType)
    {
        /* read unknown compound */
        if (memoryType is null || memoryType == typeof(Dictionary<string, object?>))
        {
            /*
             * Compound members are not necessarily ordered by their offsets. We can do one of the following to handle this:
             * - 1. sort them when the type message is being decoded
             * - 2. seek to correct offset for each individual member and seek to basePosition + Size in the end
             * - 3. sort them when defining the decode steps variable
             *
             * - Option 1 is not being choosed to keep the type definition untouched.
             * - Option 2 is also not being used because in .NET versions < .NET 6 seeking was a costly operation as there
             *     was a system call involved (https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/#summary)
             *     and we would like to avoid having specialized code for < .NET 6.
             * - Option 3 seems to be a good compromise.
             */

            var decodeSteps = Properties
                .Cast<CompoundPropertyDescription>()
                .Select(property => (property, property.MemberTypeMessage.GetDecodeInfoForScalar(context, memoryType: default).Decode))
                .OrderBy(tuple => tuple.Item1.MemberByteOffset)
                .ToArray();

            async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
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
                    result[property.Name] = await decoder(context, source).ConfigureAwait(false);
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

        /*
         * Compound members are not necessarily ordered by their offsets. We can do one of the following to handle this:
         * - 1. sort them when the type message is being decoded
         * - 2. seek to correct offset for each individual member and seek to basePosition + Size in the end
         * - 3. sort them when defining the decode steps variable
         *
         * - Option 1 is not being choosed to keep the type definition untouched.
         * - Option 2 is also not being used because in .NET versions < .NET 6 seeking was a costly operation as there
         *      was a system call involved (https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/#summary)
         *     and we would like to avoid having specialized code for < .NET 6.
         * - Option 3 seems to be a good compromise.
         */
        var compoundProperties = Properties
            .Cast<CompoundPropertyDescription>()
            .OrderBy(propertyDescription => propertyDescription.MemberByteOffset)
            .ToArray();

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

            elementDecode = (NativeReadContext context, IH5ReadStream source) =>
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

        // decode
        var factory = GetParameterlessConstructor(type);

        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
        {
            var result = factory();
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
                // The decode must run even when there is no setter, so that the source advances
                // past this member; only the assignment is conditional.
                var value = await decoder(context, source).ConfigureAwait(false);

                setValue?.Invoke(result, value);
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
        var dims = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        var elementCount = dims.Aggregate(1, (product, dim) => product * dim);

        // TODO: cache
        var invokeDecodeArray = ReadUtils.MethodInfoDecodeReferenceArray.MakeGenericMethod(elementType);
        var parameters = new object[4];

        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
        {
            parameters[0] = context;
            parameters[1] = source;
            parameters[2] = dims;
            parameters[3] = elementDecode;

            // DecodeReferenceArray is async now, so Invoke hands back a boxed ValueTask<object>.
            // Without unwrapping it here the ValueTask itself becomes the decoded element value.
            var task = (ValueTask<object>)invokeDecodeArray.Invoke(default, parameters)!;

            return await task.ConfigureAwait(false);
        }

        return decode;
    }

    private static ElementDecodeDelegate GetDecodeInfoForUnmanagedArray(
        Type elementType,
        ArrayPropertyDescription property
    )
    {
        var dims = property.DimensionSizes
            .Select(dim => (int)dim)
            .ToArray();

        // TODO: cache
        var invokeDecodeUnmanagedArray = ReadUtils.MethodInfoDecodeUnmanagedArray.MakeGenericMethod(elementType);
        var parameters = new object[2];

        // DecodeUnmanagedArray needs no context (pure unmanaged bulk read); the context
        // parameter here only exists to match ElementDecodeDelegate's shape.
        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
        {
            parameters[0] = source;
            parameters[1] = dims;

            // See GetDecodeInfoForReferenceArray: unwrap the boxed ValueTask<object>.
            var task = (ValueTask<object>)invokeDecodeUnmanagedArray.Invoke(default, parameters)!;

            return await task.ConfigureAwait(false);
        }

        return decode;
    }

    private ElementDecodeDelegate GetDecodeInfoForOpaqueAsByteArray()
    {
        var dims = new int[] { (int)Size };

        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
        {
            return await ReadUtils.DecodeUnmanagedArray<byte>(source, dims).ConfigureAwait(false);
        }

        return decode;
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForNullableValueType(
        NativeReadContext context,
        Type memoryType)
    {
        if (Properties[0] is not VariableLengthPropertyDescription property)
            throw new Exception("Variable-length properties must not be null.");

        var elementType = Nullable.GetUnderlyingType(memoryType);
        (elementType, var elementDecode) = property.BaseType.GetDecodeInfoForScalar(context, elementType);

        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
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

            using var memoryOwner = new ScratchBuffer<byte>(totalSize);
            var buffer = memoryOwner.Memory[0..totalSize];

            await source.ReadDatasetAsync(buffer).ConfigureAwait(false);

            /* decode sequence length */
            var sequenceLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Span);

            buffer = buffer.Slice(lengthSize);

            /* decode global heap IDs and get associated data */
            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

            if (globalHeapId.Equals(default))
                return default;

            if (sequenceLength != 1)
                throw new Exception("Only variable-length sequence with length = 1 can be decoded into a nullable value type.");

            buffer = buffer.Slice(globalHeapIdSize);

            var globalHeapCollection = await NativeCache.GetGlobalHeapObject(
                context,
                globalHeapId.CollectionAddress,
                restoreAddress: true).ConfigureAwait(false);

            if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
            {
                // TODO: cache short-lived stream?
                var localSource = new SystemMemoryStream(globalHeapObject.ObjectData);
                var value = (await elementDecode(context, localSource).ConfigureAwait(false))!;

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

        return (memoryType, decode);
    }

    private (Type, ElementDecodeDelegate) GetDecodeInfoForVariableLengthSequence(
        NativeReadContext context,
        Type? memoryType)
    {
        if (Properties[0] is not VariableLengthPropertyDescription property)
            throw new Exception("Variable-length properties must not be null.");

        var elementType = memoryType?.GetElementType();
        (elementType, var elementDecode) = property.BaseType.GetDecodeInfoForScalar(context, elementType);

        // Fast path: blittable element type whose in-memory size matches the on-disk size.
        // Eliminates per-element boxing and the staging object[] allocation by casting the
        // global-heap object bytes directly into a freshly allocated typed array.
        if (!DataUtils.IsReferenceOrContainsReferences(elementType) &&
            !property.BaseType.IsReferenceOrContainsReferences() &&
            DataUtils.UnmanagedSizeOf(elementType) == (int)property.BaseType.Size)
        {
            var fastDecode = (ElementDecodeDelegate)_methodInfoBuildVariableLengthSequenceUnmanagedDecoder
                .MakeGenericMethod(elementType)
                .Invoke(default, [(int)property.BaseType.Size])!;

            memoryType ??= Type.GetType($"{elementType}[]")
                ?? throw new Exception($"Unable to find array type for element type {elementType}.");

            return (memoryType, fastDecode);
        }

        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
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

            var header = await TryReadVariableLengthHeader(context, source).ConfigureAwait(false);

            if (!header.Success)
                return default;

            var sequenceLength = header.SequenceLength;
            var objectData = header.ObjectData;

            var array = Array.CreateInstance(elementType, sequenceLength);

            // TODO: cache short-lived stream?
            var localSource = new SystemMemoryStream(objectData);

            for (int i = 0; i < sequenceLength; i++)
            {
                array.SetValue(await elementDecode(context, localSource).ConfigureAwait(false), i);
            }

            return array;
        }

        memoryType ??= Type.GetType($"{elementType}[]")
            ?? throw new Exception($"Unable to find array type for element type {elementType}.");

        return (memoryType, decode);
    }

    private ElementDecodeDelegate GetDecodeInfoForVariableLengthString()
    {
        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
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
            using var memoryOwner = new ScratchBuffer<byte>(totalSize);
            var buffer = memoryOwner.Memory[0..totalSize];

            await source.ReadDatasetAsync(buffer).ConfigureAwait(false);

            /* skip the length of the sequence (H5Tvlen.c H5T_vlen_disk_read) */
            buffer = buffer.Slice(sizeof(uint));

            /* decode global heap IDs and get associated data */
            var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, buffer.Span);

            if (globalHeapId.Equals(default))
                return default;

            var globalHeapCollection = await NativeCache.GetGlobalHeapObject(
                context,
                globalHeapId.CollectionAddress,
                restoreAddress: true).ConfigureAwait(false);

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
                // but that would make the following test fail
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
        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
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
                PaddingType.NullTerminate => value => value.Split('\0', 2)[0],
                PaddingType.NullPad => value => value.TrimEnd('\0'),
                PaddingType.SpacePad => value => value.TrimEnd(' '),
                _ => throw new Exception("Unsupported padding type.")
            };

            using var memoryOwner = new ScratchBuffer<byte>((int)Size);
            var memory = memoryOwner.Memory[0..(int)Size];

            await source.ReadDatasetAsync(memory).ConfigureAwait(false);

            // Decoded as UTF-8, which is correct for H5T_CSET_ASCII too, and matches
            // GetDecodeInfoForVariableLengthString. See ReadUtils.
            var value = ReadUtils.ReadFixedLengthString(memory.Span);
            value = trim(value);

            return value;
        }

        return decode;
    }

    private bool IsNullableValueTypeAndCanDecode<TElement>()
    {
        var underlyingType = Nullable.GetUnderlyingType(typeof(TElement));

        if (underlyingType is null)
            return false;

        var underlyingTypeSize = Marshal.SizeOf(underlyingType);

        if (Class == DatatypeMessageClass.VariableLength)
        {
            var variableLengthType = ((VariableLengthBitFieldDescription)BitField).Type;

            if (variableLengthType != InternalVariableLengthType.Sequence)
                return false;

            var variableLengthBaseType = ((VariableLengthPropertyDescription)Properties[0])
                .BaseType;

            if (variableLengthBaseType.IsReferenceOrContainsReferences() || 
                variableLengthBaseType.Size != underlyingTypeSize)
                return false;           
        }

        // maybe more type classes should be supported in future
        else
        {
            return false;
        }

        return true;
    }

    private DecodeDelegate<T> GetDecodeInfoForReferenceMemory<T>(
        NativeReadContext context
    )
    {
        var elementDecode = GetDecodeInfoForScalar(context, typeof(T)).Decode;

        // Variable-length sequences and strings store a fixed-size (length + global
        // heap id) header per cell in the dataset stream, with the payload living
        // in the global heap. The per-cell element decoder reads that header via
        // source.ReadDatasetAsync(headerBytes) before resolving the heap object — and on
        // an N-cell decode pass that becomes N small ReadDataset calls into the
        // underlying IH5ReadStream. Pre-reading all N headers in one bulk call and
        // feeding the per-cell decoder from an in-memory wrapper collapses the
        // per-call dispatch + position-tracking overhead. The per-cell element
        // decoder itself is unchanged.

        if (Class == DatatypeMessageClass.VariableLength)
        {
            var cellHeaderSize = sizeof(uint) + context.Superblock.OffsetsSize + sizeof(uint);

            // FAST PATH (#163): one bulk ReadDataset for all N cell headers, ArrayPool-rented,
            // then per-cell decode from an in-memory wrapper. Preserved exactly; the only change
            // is that the bulk read is awaited instead of blocking.
            async ValueTask decodeBatched(NativeReadContext context, IH5ReadStream source, Memory<T> target)
            {
                if (target.Length == 0)
                    return;

                var totalBytes = target.Length * cellHeaderSize;

                using var memoryOwner = new ScratchBuffer<byte>(totalBytes);
                var bulk = memoryOwner.Memory[..totalBytes];

                await source.ReadDatasetAsync(bulk).ConfigureAwait(false);

                var localSource = new SystemMemoryStream(bulk);

                for (int i = 0; i < target.Length; i++)
                {
                    var element = await elementDecode(context, localSource).ConfigureAwait(false);
                    target.Span[i] = (T)element!;
                }
            }

            return decodeBatched;
        }

        else
        {
            // Reference/compound/array types: each element decode issues multiple
            // ReadDatasetAsync + Seek calls against the source (per compound member,
            // per array element, per reference resolution). When the source is a live
            // driver stream, a batch of N elements over M members costs N*M small
            // dispatch calls into the driver - one per member per element.
            //
            // The bulk read below helps exactly when ALL of these hold:
            //   - Layout is contiguous (compact/chunked already return a SystemMemoryStream
            //     from GetReadStream, so their source is in-memory and IsBuffered).
            //   - The source is a live driver stream (file/mmap driver for a contiguous
            //     dataset), i.e. NOT already buffered - IsBuffered is false.
            //   - The read type is a reference/compound/array (non-VL), so this else
            //     branch is taken. VL has its own decodeBatched above; a blittable
            //     struct of matching size takes the zero-copy unmanaged fast path
            //     earlier and never reaches here.
            //
            // Under those conditions, bulk-reading the entire batch into a pooled
            // buffer and decoding from an in-memory SystemMemoryStream wrapper collapses
            // N*M driver dispatches into one driver read + N*M cheap in-memory copies,
            // matching the pattern already used by compact/chunked and by the VL path.
            // On the benchmark (2000 elements x 6 members) this is a 7-8x speedup.
            //
            // Sources that are already in-memory (IsBuffered) skip the bulk read: it
            // would just duplicate the copy that the per-element decode already does.

            var elementSize = (int)Size;

            async ValueTask decode(NativeReadContext context, IH5ReadStream source, Memory<T> target)
            {
                if (target.Length == 0)
                    return;

                if (source.IsBuffered)
                {
                    for (int i = 0; i < target.Length; i++)
                    {
                        var element = await elementDecode(context, source).ConfigureAwait(false);
                        target.Span[i] = (T)element!;
                    }

                    return;
                }

                var totalBytes = target.Length * elementSize;

                using var memoryOwner = new ScratchBuffer<byte>(totalBytes);
                var bulk = memoryOwner.Memory[..totalBytes];

                await source.ReadDatasetAsync(bulk).ConfigureAwait(false);

                var localSource = new SystemMemoryStream(bulk);

                for (int i = 0; i < target.Length; i++)
                {
                    var element = await elementDecode(context, localSource).ConfigureAwait(false);
                    target.Span[i] = (T)element!;
                }
            };

            return decode;
        }
    }

    private static DecodeDelegate<T> GetDecodeInfoForUnmanagedMemory<T>()
        where T : struct
    {
        // HOT PATH: plain unmanaged (numeric) datasets, which must reach
        //     source.ReadDatasetAsync(MemoryMarshal.AsBytes(target))
        // i.e. a single zero-copy read straight into the caller's buffer. Span<T> cannot be
        // reinterpreted as Memory<byte>, so both overloads below preserve the zero-copy read rather
        // than falling back to a pooled rent plus a full copy.
        //
        // The Span overload is tried first because it matches the baseline exactly and allocates
        // nothing. The Memory overload needs `Cast`, which heap-allocates a CastMemoryManager per
        // call (~32 bytes) - measurable on scalar-dense reads - so it is reserved for sources that
        // genuinely suspend.
        //
        // The context parameter below is unused - this decoder captures no context at all, same
        // as before - it only exists to match DecodeDelegate<T>'s shape. `static` is preserved so
        // the local function captures nothing.
        static ValueTask decode(NativeReadContext context, IH5ReadStream source, Memory<T> target)
        {
            if (source.TryReadDatasetSync(MemoryMarshal.AsBytes(target.Span)))
                return default;

            return source.ReadDatasetAsync(target.Cast<T, byte>());
        }

        return decode;
    }

    /// <summary>
    /// The result of <see cref="TryReadVariableLengthHeader" />. <c>default</c> means "not present",
    /// which is the two failure cases the caller treats identically.
    /// </summary>
    private readonly record struct VariableLengthHeader(
        bool Success,
        uint SequenceLength,
        byte[] ObjectData);

    // The header is a 4-byte length plus a global-heap id, and an id is an offset (at most 8 bytes)
    // plus a 4-byte index. Anything larger than this falls to the pooled slow path rather than
    // silently overflowing.
    private const int MaxVariableLengthHeaderSize = sizeof(uint) + 8 + sizeof(uint);

    /// <summary>
    /// Reads the length + global-heap-id header that precedes every variable-length element and
    /// resolves the heap object it points at.
    /// </summary>
    /// <remarks>
    /// Every variable-length decode goes through here, so this must not block: if it did, a
    /// variable-length read would block on I/O no matter how correct the layers above it were. Both of
    /// its reads are therefore awaited, and the two results come back in a struct rather than through
    /// <c>out</c> parameters, which cannot coexist with <c>async</c> (CS1988) - the same shape used for
    /// the same reason elsewhere in the read path.
    /// <para>
    /// Not <c>async</c> itself. This sits in the innermost decode loop - once per element - so the
    /// common case must not pay for a state machine: a local source reads the header without
    /// suspending and the heap collection is almost always already cached, in which case the whole
    /// method completes synchronously and allocates nothing. The header also moves from a pooled buffer
    /// to <c>stackalloc</c>, since it is at most 16 bytes.
    /// </para>
    /// </remarks>
    private static ValueTask<VariableLengthHeader> TryReadVariableLengthHeader(
        NativeReadContext context,
        IH5ReadStream source)
    {
        var lengthSize = sizeof(uint);
        var globalHeapIdSize = (int)context.Superblock.OffsetsSize + sizeof(uint);
        var headerSize = lengthSize + globalHeapIdSize;

        if (headerSize <= MaxVariableLengthHeaderSize)
        {
            Span<byte> headerBuffer = stackalloc byte[MaxVariableLengthHeaderSize];
            var header = headerBuffer[..headerSize];

            if (source.TryReadDatasetSync(header))
            {
                var sequenceLength = BinaryPrimitives.ReadUInt32LittleEndian(header);
                var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, header[lengthSize..]);

                if (globalHeapId.Equals(default))
                    return new ValueTask<VariableLengthHeader>(default(VariableLengthHeader));

                var pending = NativeCache.GetGlobalHeapObject(
                    context,
                    globalHeapId.CollectionAddress,
                    restoreAddress: true);

                if (pending.IsCompletedSuccessfully)
                    return new ValueTask<VariableLengthHeader>(Resolve(pending.Result, globalHeapId, sequenceLength));

                return AwaitCollection(pending, globalHeapId, sequenceLength);
            }
        }

        return ReadSlow(context, source, headerSize, lengthSize);
    }

    private static async ValueTask<VariableLengthHeader> AwaitCollection(
        ValueTask<GlobalHeapCollection> pending,
        ReadingGlobalHeapId globalHeapId,
        uint sequenceLength)
    {
        var collection = await pending.ConfigureAwait(false);

        return Resolve(collection, globalHeapId, sequenceLength);
    }

    // The source cannot be read without suspending, so the buffer has to survive an await and cannot
    // be a stackalloc'd Span.
    private static async ValueTask<VariableLengthHeader> ReadSlow(
        NativeReadContext context,
        IH5ReadStream source,
        int headerSize,
        int lengthSize)
    {
        using var memoryOwner = new ScratchBuffer<byte>(headerSize);
        var headerBuffer = memoryOwner.Memory[..headerSize];

        await source.ReadDatasetAsync(headerBuffer).ConfigureAwait(false);

        var sequenceLength = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.Span);
        var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, headerBuffer.Span[lengthSize..]);

        if (globalHeapId.Equals(default))
            return default;

        var collection = await NativeCache.GetGlobalHeapObject(
            context,
            globalHeapId.CollectionAddress,
            restoreAddress: true).ConfigureAwait(false);

        return Resolve(collection, globalHeapId, sequenceLength);
    }

    private static VariableLengthHeader Resolve(
        GlobalHeapCollection collection,
        ReadingGlobalHeapId globalHeapId,
        uint sequenceLength)
    {
        if (!collection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
            return default;

        return new VariableLengthHeader(true, sequenceLength, globalHeapObject.ObjectData);
    }

    private static ElementDecodeDelegate BuildVariableLengthSequenceUnmanagedDecoder<TElement>(
        int fileTypeSize)
        where TElement : unmanaged
    {
        async ValueTask<object?> decode(NativeReadContext context, IH5ReadStream source)
        {
            var header = await TryReadVariableLengthHeader(context, source).ConfigureAwait(false);

            if (!header.Success)
                return default;

            var sequenceLength = header.SequenceLength;
            var objectData = header.ObjectData;

            var count = (int)sequenceLength;
            var result = GC.AllocateUninitializedArray<TElement>(count);

            if (count == 0)
                return result;

            var byteCount = count * fileTypeSize;
            MemoryMarshal.Cast<byte, TElement>(objectData.AsSpan(0, byteCount)).CopyTo(result);

            return result;
        }

        return decode;
    }
}
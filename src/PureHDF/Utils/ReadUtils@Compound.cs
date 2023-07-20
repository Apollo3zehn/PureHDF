using System.Buffers;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PureHDF;

internal static partial class ReadUtils
{
    public static unsafe Memory<T> ReadCompound<T>(
        NativeContext context,
        DatatypeMessage datatype,
        Span<byte> data,
        Memory<T> destination,
        Func<FieldInfo, string?> getName) where T : struct
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
            var name = getName(fieldInfo) ?? fieldInfo.Name;

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

        var destinationElementSize = Marshal.SizeOf<T>();

        using var destinationRawBytesOwner = MemoryPool<byte>.Shared.Rent(destinationElementSize);
        var destinationRawBytes = destinationRawBytesOwner.Memory[..destinationElementSize];
        destinationRawBytes.Span.Clear();

        var stringMap = new Dictionary<FieldProperties, string?>();

        for (int i = 0; i < destination.Length; i++)
        {
            stringMap.Clear();

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
                        .CopyTo(destinationRawBytes.Span.Slice(targetIndex, fieldSize));
                }
            }

            sourceOffset += sourceElementSize;
            var destinationSpan = destination.Span;

            fixed (byte* ptr = destinationRawBytes.Span)
            {
                // http://benbowen.blog/post/fun_with_makeref/
                // https://stackoverflow.com/questions/4764573/why-is-typedreference-behind-the-scenes-its-so-fast-and-safe-almost-magical
                // Both do not work because struct layout is different with __makeref:
                // https://stackoverflow.com/questions/1918037/layout-of-net-value-type-in-memory
                destinationSpan[i] = Marshal.PtrToStructure<T>(new IntPtr(ptr));

                foreach (var entry in stringMap)
                {
                    var reference = __makeref(destinationSpan[i]);
                    entry.Key.FieldInfo.SetValueDirect(reference, entry.Value!);
                }
            }
        }

        return destination;
    }

    public static Dictionary<string, object?>[] ReadCompound(
        NativeContext context,
        DatatypeMessage datatype,
        Span<byte> data)
    {
        var size = (int)datatype.Size;
        var elementCount = data.Length / size;
        var destination = new Dictionary<string, object?>[elementCount];

        ReadCompound(context, datatype, data, destination);

        return destination;
    }

    public static unsafe Memory<Dictionary<string, object?>> ReadCompound(
        NativeContext context,
        DatatypeMessage datatype,
        Span<byte> data,
        Memory<Dictionary<string, object?>> destination)
    {
        if (datatype.Class != DatatypeMessageClass.Compound)
            throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Compound}'.");

        var destinationSpan = destination.Span;

        var members = datatype.Properties
            .Cast<CompoundPropertyDescription>()
            .ToList();

        var sourceOffset = 0UL;
        var sourceElementSize = datatype.Size;

        using var oneElementStringArrayOwner = MemoryPool<string>.Shared.Rent(1);
        var oneElementStringArray = oneElementStringArrayOwner.Memory[..1];

        using var oneElementCompoundArrayOwner = MemoryPool<Dictionary<string, object?>>.Shared.Rent(1);
        var oneElementCompoundArray = oneElementCompoundArrayOwner.Memory[..1];

        for (int i = 0; i < destination.Length; i++)
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
                        => ReadString(context, memberType, slicedData, oneElementStringArray).Span[0],

                    // TODO: Bitfield padding type is not being applied here as well as in the normal Read<T> method.
                    (DatatypeMessageClass.BitField, _)
                        => slicedData.ToArray(),

                    (DatatypeMessageClass.Opaque, _)
                        => slicedData.ToArray(),

                    (DatatypeMessageClass.Compound, _)
                        => ReadCompound(context, memberType, slicedData, oneElementCompoundArray).Span[0],

                    // TODO: Reference type (from the bit field) is not being considered here as well as in the normal Read<T> method.
                    (DatatypeMessageClass.Reference, _)
                        => MemoryMarshal.Cast<byte, NativeObjectReference1>(slicedData)[0],

                    /* It is difficult to avoid array allocation here */
                    (DatatypeMessageClass.Enumerated, _)
                        => (ReadEnumerated(context, memberType, slicedData) ?? new object[1]).GetValue(0),

                    (DatatypeMessageClass.VariableLength, _) when variableLengthBitfield!.Type == InternalVariableLengthType.String
                        => ReadString(context, memberType, slicedData, oneElementStringArray).Span[0],

                    (DatatypeMessageClass.Array, _)
                        => ReadArray(context, memberType, slicedData),

                    _ => default
                };
            }

            destinationSpan[i] = map;
            sourceOffset += sourceElementSize;
        }

        return destination;
    }
}
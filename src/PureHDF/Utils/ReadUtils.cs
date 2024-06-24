using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF;

internal static partial class ReadUtils
{
    public static MethodInfo MethodInfoDecodeUnmanagedElement { get; } = typeof(ReadUtils)
        .GetMethod(nameof(DecodeUnmanagedElement), BindingFlags.Public | BindingFlags.Static)!;

    public static MethodInfo MethodInfoDecodeReferenceArray { get; } = typeof(ReadUtils)
        .GetMethod(nameof(DecodeReferenceArray), BindingFlags.Public | BindingFlags.Static)!;

    public static MethodInfo MethodInfoDecodeUnmanagedArray { get; } = typeof(ReadUtils)
        .GetMethod(nameof(DecodeUnmanagedArray), BindingFlags.Public | BindingFlags.Static)!;

    public static ulong ReadUlong(Span<byte> buffer, ulong size)
    {
        return size switch
        {
            1 => buffer[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(buffer),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(buffer),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(buffer),
            _ => ReadUlongArbitrary(buffer, size)
        };
    }

    public static ulong ReadUlong(H5DriverBase driver, ulong size)
    {
        return size switch
        {
            1 => driver.ReadByte(),
            2 => driver.ReadUInt16(),
            4 => driver.ReadUInt32(),
            8 => driver.ReadUInt64(),
            _ => ReadUlongArbitrary(driver, size)
        };
    }

    private static ulong ReadUlongArbitrary(Span<byte> buffer, ulong size)
    {
        var result = 0UL;
        var shift = 0;

        for (ulong i = 0; i < size; i++)
        {
            var value = buffer[0];
            buffer = buffer.Slice(1);
            result += (ulong)(value << shift);
            shift += 8;
        }

        return result;
    }

    private static ulong ReadUlongArbitrary(H5DriverBase driver, ulong size)
    {
        var result = 0UL;
        var shift = 0;

        for (ulong i = 0; i < size; i++)
        {
            var value = driver.ReadByte();
            result += (ulong)(value << shift);
            shift += 8;
        }

        return result;
    }

    public static bool CanDecodeFromCompound(Type type)
    {
        if (type.IsValueType)
            return !(type.IsPrimitive || type.IsEnum);

        else
            return !type.IsArray;
    }

    public static bool CanDecodeToUnmanaged(
        Type type,
        int fileTypeSize)
    {
        if (DataUtils.IsReferenceOrContainsReferences(type))
            return false;

        var typeSize = DataUtils.UnmanagedSizeOf(type);

        return typeSize == fileTypeSize;
    }

    public static (Memory<TElement>, ulong[]) ToMemory<TResult, TElement>(TResult buffer)
    {
        var type = typeof(TResult);

        if (DataUtils.IsMemory(type))
        {
            var memory = (Memory<TElement>)(object)buffer!;
            return (memory, [(ulong)memory.Length]);
        }

        else if (DataUtils.IsArray(type))
        {
            var array = (Array)(object)buffer!;
            var memory = new ArrayMemoryManager<TElement>(array).Memory;

            var dimensions = Enumerable
                .Range(0, array.Rank)
                .Select(dim => (ulong)array.GetLongLength(dim))
                .ToArray();

            return (memory, dimensions);
        }

        else
        {
            var memory = new TElement[] { (TElement)(object)buffer! };
            return (memory, [1]);
        }
    }

    public static TResult FromArray<TResult, TElement>(Array buffer)
    {
        var type = typeof(TResult);

        if (DataUtils.IsArray(type))
            return (TResult)(object)buffer;

        else
            return (TResult)buffer.GetValue(0)!;
    }

    public static T DecodeUnmanagedElement<T>(IH5ReadStream source) where T : struct
    {
        var bytesOfType = Unsafe.SizeOf<T>();
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(bytesOfType);
        var buffer = memoryOwner.Memory[..bytesOfType];

        source.ReadDataset(buffer.Span);

        return MemoryMarshal.Cast<byte, T>(buffer.Span)[0];
    }

    public static object DecodeReferenceArray<TElement>(IH5ReadStream source, int[] dims, ElementDecodeDelegate elementDecode)
    {
        var array = Array.CreateInstance(typeof(TElement), dims);
        var span = new ArrayMemoryManager<TElement>(array).Memory.Span;

        for (int index = 0; index < array.Length; index++)
        {
            span[index] = (TElement)elementDecode(source)!;
        }

        return array;
    }

    public static object DecodeUnmanagedArray<TElement>(IH5ReadStream source, int[] dims)
        where TElement : unmanaged
    {
        var array = Array.CreateInstance(typeof(TElement), dims);
        var memory = new ArrayMemoryManager<TElement>(array).Memory;

        source.ReadDataset(MemoryMarshal.AsBytes(memory.Span));

        return array;
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

    public static string ReadFixedLengthString(H5DriverBase driver, int length, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
    {
        var data = driver.ReadBytes(length);

        return encoding switch
        {
            CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
            CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
            _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
        };
    }

    public static string ReadNullTerminatedString(H5DriverBase driver, bool pad, int padSize = 8, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
    {
        var data = new List<byte>();
        var byteValue = driver.ReadByte();

        while (byteValue != '\0')
        {
            data.Add(byteValue);
            byteValue = driver.ReadByte();
        }

        var destination = encoding switch
        {
            CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data.ToArray()),
            CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data.ToArray()),
            _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
        };

        if (pad)
        {
            // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
            var paddingCount = (padSize - (destination.Length + 1) % padSize) % padSize;
            driver.Seek(paddingCount, SeekOrigin.Current);
        }

        return destination;
    }
}
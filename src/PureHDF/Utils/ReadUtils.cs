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

    /* Strings are always decoded as UTF-8, never as ASCII.
     *
     * H5T_cset_t defines only H5T_CSET_ASCII and H5T_CSET_UTF8, and ASCII is a strict
     * subset of UTF-8 — all 128 ASCII byte values decode identically under both — so a
     * UTF-8 decoder is correct for every conformant payload, including the fields the
     * format specification fixes as ASCII (filter names, driver identifiers, dates).
     *
     * Where the two differ is a payload that holds UTF-8 while being declared, or defaulted
     * to, ASCII — which is what any writer that does not set a character set produces.
     * There, Encoding.ASCII replaces every byte >= 0x80 with '?' silently and
     * irrecoverably, and leaves the result indistinguishable from a literal '?'. UTF-8
     * recovers such a payload, and marks genuinely malformed bytes U+FFFD.
     */
    public static string ReadFixedLengthString(Span<byte> data)
    {
        return Encoding.UTF8.GetString(data);
    }

    public static string ReadFixedLengthString(H5DriverBase driver, int length)
    {
        var data = driver.ReadBytes(length);

        return Encoding.UTF8.GetString(data);
    }

    public static string ReadNullTerminatedString(H5DriverBase driver, bool pad, int padSize = 8)
    {
        var data = new List<byte>();
        var byteValue = driver.ReadByte();

        while (byteValue != '\0')
        {
            data.Add(byteValue);
            byteValue = driver.ReadByte();
        }

        var destination = Encoding.UTF8.GetString(data.ToArray());

        if (pad)
        {
            // The padding is measured from the bytes on disk, not from the decoded string:
            // a multi-byte character makes the string shorter than the data it came from,
            // which would seek to the wrong offset and desynchronise the driver.
            // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
            var paddingCount = (padSize - (data.Count + 1) % padSize) % padSize;
            driver.Seek(paddingCount, SeekOrigin.Current);
        }

        return destination;
    }
}
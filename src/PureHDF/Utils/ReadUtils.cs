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

    public static async ValueTask<ulong> ReadUlong(H5DriverBase driver, ulong size)
    {
        return size switch
        {
            1 => await driver.ReadByte().ConfigureAwait(false),
            2 => await driver.ReadUInt16().ConfigureAwait(false),
            4 => await driver.ReadUInt32().ConfigureAwait(false),
            8 => await driver.ReadUInt64().ConfigureAwait(false),
            _ => await ReadUlongArbitrary(driver, size).ConfigureAwait(false)
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

    private static async ValueTask<ulong> ReadUlongArbitrary(H5DriverBase driver, ulong size)
    {
        var result = 0UL;
        var shift = 0;

        for (ulong i = 0; i < size; i++)
        {
            var value = await driver.ReadByte().ConfigureAwait(false);
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

    public static async ValueTask<T> DecodeUnmanagedElement<T>(IH5ReadStream source) where T : struct
    {
        var bytesOfType = Unsafe.SizeOf<T>();
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(bytesOfType);
        var buffer = memoryOwner.Memory[..bytesOfType];

        await source.ReadDataset(buffer).ConfigureAwait(false);

        return MemoryMarshal.Cast<byte, T>(buffer.Span)[0];
    }

    // NOTE (async-first): the per-element decode is awaited, so the destination is held as
    // Memory<T> and indexed through .Span per iteration (a Span local cannot survive an await).
    public static async ValueTask<object> DecodeReferenceArray<TElement>(IH5ReadStream source, int[] dims, ElementDecodeDelegate elementDecode)
    {
        var array = Array.CreateInstance(typeof(TElement), dims);
        var memory = new ArrayMemoryManager<TElement>(array).Memory;

        for (int index = 0; index < array.Length; index++)
        {
            var element = await elementDecode(source).ConfigureAwait(false);
            memory.Span[index] = (TElement)element!;
        }

        return array;
    }

    public static async ValueTask<object> DecodeUnmanagedArray<TElement>(IH5ReadStream source, int[] dims)
        where TElement : unmanaged
    {
        var array = Array.CreateInstance(typeof(TElement), dims);
        var memory = new ArrayMemoryManager<TElement>(array).Memory;
        var byteLength = memory.Length * Unsafe.SizeOf<TElement>();

        using var memoryOwner = MemoryPool<byte>.Shared.Rent(byteLength);
        var buffer = memoryOwner.Memory[..byteLength];

        await source.ReadDataset(buffer).ConfigureAwait(false);

        MemoryMarshal.Cast<byte, TElement>(buffer.Span).CopyTo(memory.Span);

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

    public static async ValueTask<string> ReadFixedLengthString(H5DriverBase driver, int length)
    {
        var data = await driver.ReadBytes(length).ConfigureAwait(false);

        return Encoding.UTF8.GetString(data);
    }

    public static async ValueTask<string> ReadNullTerminatedString(H5DriverBase driver, bool pad, int padSize = 8)
    {
        var data = new List<byte>();
        var byteValue = await driver.ReadByte().ConfigureAwait(false);

        while (byteValue != '\0')
        {
            data.Add(byteValue);
            byteValue = await driver.ReadByte().ConfigureAwait(false);
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
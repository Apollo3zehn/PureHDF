using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal delegate void EncodeDelegate<T>(Memory<T> source, IH5WriteStream target);

internal record GlobalHeapCollectionState(
    GlobalHeapCollection Collection, 
    Memory<byte> Memory)
{
    public int Consumed { get; set; }
};

internal record WriteContext(
    BinaryWriter Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5SerializerOptions SerializerOptions,
    Dictionary<Type, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<object, ulong> ObjectToAddressMap
);

[StructLayout(LayoutKind.Explicit, Size = 12)]
internal record struct WritingGlobalHeapId(
    [field: FieldOffset(0)] ulong Address, 
    [field: FieldOffset(8)] uint Index);

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal record struct VariableLengthElement(
    [field: FieldOffset(0)] uint Length,
    [field: FieldOffset(4)] WritingGlobalHeapId HeapId);

internal class UnmanagedArrayMemoryManager<T> : MemoryManager<T> where T : struct
{
    private readonly Array _array;

    public UnmanagedArrayMemoryManager(Array array)
    {
        _array = array;
    }

    public override Span<T> GetSpan()
    {
#if NET6_0_OR_GREATER
        var span = MemoryMarshal.CreateSpan(
            reference: ref MemoryMarshal.GetArrayDataReference(_array), 
            length: _array.Length * Unsafe.SizeOf<T>());

        return MemoryMarshal.Cast<byte, T>(span);
#else
        if (_array.Rank != 1)
            throw new Exception("Multi-dimensions arrays are only supported on .NET 6+.");

        else
            return ((T[])_array).AsSpan();
#endif
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        throw new NotImplementedException();
    }

    public override void Unpin()
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        throw new NotImplementedException();
    }
}
using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal delegate void EncodeDelegate<T>(Memory<T> source, IH5WriteStream target);
internal delegate void ElementEncodeDelegate(object source, IH5WriteStream target);

internal record GlobalHeapCollectionState(
    GlobalHeapCollection Collection,
    Memory<byte> Memory)
{
    public int Consumed { get; set; }
};

[StructLayout(LayoutKind.Explicit, Size = 12)]
internal record struct WritingGlobalHeapId(
    [field: FieldOffset(0)] ulong Address,
    [field: FieldOffset(8)] uint Index);

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal record struct VariableLengthElement(
    [field: FieldOffset(0)] uint Length,
    [field: FieldOffset(4)] WritingGlobalHeapId HeapId);
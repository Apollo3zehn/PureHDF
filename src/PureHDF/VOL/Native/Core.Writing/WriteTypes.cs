using System.Runtime.InteropServices;

namespace PureHDF.VOL.Native;

internal delegate void EncodeDelegate<T>(Memory<T> source, IH5WriteStream target);
internal delegate void ElementEncodeDelegate(object source, IH5WriteStream target);

internal record GlobalHeapCollectionState(
    GlobalHeapCollection Collection,
    Memory<byte> Memory,
    long BaseAddress)
{
    public int Consumed { get; set; }

    // Per collection rather than per manager: object indices restart at 1 in each collection, and the
    // manager keeps one collection open per allocation kind, so a single counter would interleave
    // two collections' indices and mislabel their objects.
    public ushort Index { get; set; }
};

[StructLayout(LayoutKind.Explicit, Size = 12)]
internal record struct WritingGlobalHeapId(
    [field: FieldOffset(0)] ulong Address,
    [field: FieldOffset(8)] uint Index);

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal record struct VariableLengthElement(
    [field: FieldOffset(0)] uint Length,
    [field: FieldOffset(4)] WritingGlobalHeapId HeapId);
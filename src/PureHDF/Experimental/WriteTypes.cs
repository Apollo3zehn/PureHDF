using System.Runtime.InteropServices;

namespace PureHDF.Experimental;

internal record GlobalHeapCollectionState(
    GlobalHeapCollection Collection, 
    Memory<byte> Memory)
{
    public int Consumed { get; set; }
};

internal record WriteContext(
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5SerializerOptions SerializerOptions,
    Dictionary<Type, (DatatypeMessage, EncodeDelegate)> TypeToMessageMap,
    Dictionary<H5Object, ulong> ObjectToAddressMap
);

[StructLayout(LayoutKind.Explicit, Size = 12)]
internal record struct GlobalHeapId(
    [field: FieldOffset(0)] ulong Address, 
    [field: FieldOffset(8)] uint Index);

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal record struct VariableLengthElement(
    [field: FieldOffset(0)] uint Length,
    [field: FieldOffset(4)] GlobalHeapId HeapId);
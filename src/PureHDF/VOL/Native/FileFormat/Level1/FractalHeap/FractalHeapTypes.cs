namespace PureHDF.VOL.Native;

internal enum FractalHeapIdType : byte
{
    Managed = 0,
    Huge = 1,
    Tiny = 2
}

[Flags]
internal enum FractalHeapHeaderFlags : byte
{
    IdValueIsWrapped = 1,
    DirectBlocksAreChecksummed = 2
}

internal readonly record struct FractalHeapEntry(
    ulong Address,
    ulong FilteredSize,
    uint FilterMask
);
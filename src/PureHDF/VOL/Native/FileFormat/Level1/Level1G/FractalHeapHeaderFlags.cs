namespace PureHDF.VOL.Native;

[Flags]
internal enum FractalHeapHeaderFlags : byte
{
    IdValueIsWrapped = 1,
    DirectBlocksAreChecksummed = 2
}
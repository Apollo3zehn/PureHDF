namespace PureHDF.VOL.Native;

internal abstract record class FractalHeapId(
//
)
{
    internal static async ValueTask<FractalHeapId> Construct(
        NativeReadContext context,
        H5DriverBase localDriver,
        FractalHeapHeader header)
    {
        var firstByte = await localDriver.ReadByte().ConfigureAwait(false);

        // bits 6-7
        var version = (byte)((firstByte & 0xB0) >> 6);

        if (version != 0)
            throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapId)} are supported.");

        // bits 4-5
        var type = (FractalHeapIdType)((firstByte & 0x30) >> 4);

        // offset and length Size (for managed objects fractal heap id)
        var offsetSize = (ulong)Math.Ceiling(header.MaximumHeapSize / 8.0);
        // TODO: Is -1 correct?
        var lengthSize = MathUtils.FindMinByteCount(header.MaximumDirectBlockSize - 1);

        // H5HF.c (H5HF_op)
        return (FractalHeapId)((type, header.HugeIdsAreDirect, header.IOFilterEncodedLength, header.TinyObjectsAreExtended) switch
        {
            (FractalHeapIdType.Managed, _, _, _) => await ManagedObjectsFractalHeapId.Decode(context, localDriver, header, offsetSize, lengthSize).ConfigureAwait(false),

            // H5HFhuge.c (H5HF__huge_op_real)
            (FractalHeapIdType.Huge, false, 0, _) => await HugeObjectsFractalHeapIdSubType1.Decode(context, localDriver, header).ConfigureAwait(false),
            (FractalHeapIdType.Huge, false, _, _) => await HugeObjectsFractalHeapIdSubType2.Decode(context, localDriver, header).ConfigureAwait(false),
            (FractalHeapIdType.Huge, true, 0, _) => await HugeObjectsFractalHeapIdSubType3.Decode(context, localDriver).ConfigureAwait(false),
            (FractalHeapIdType.Huge, true, _, _) => await HugeObjectsFractalHeapIdSubType4.Decode(context.Superblock, localDriver).ConfigureAwait(false),

            // H5HFtiny.c (H5HF_tiny_op_real)
            (FractalHeapIdType.Tiny, _, _, false) => await TinyObjectsFractalHeapIdSubType1.Decode(localDriver, firstByte).ConfigureAwait(false),
            (FractalHeapIdType.Tiny, _, _, true) => await TinyObjectsFractalHeapIdSubType2.Decode(localDriver, firstByte).ConfigureAwait(false),

            // default
            _ => throw new Exception($"Unknown heap ID type '{type}'.")
        });
    }

    // Async, and takes no cache parameter. Locating a heap object is itself a read - a managed ID
    // walks the heap's indirect blocks, a huge ID consults a b-tree - so a synchronous Read here would
    // block every dense attribute and dense link decode no matter how carefully the layers above it
    // awaited. A `ref` parameter for the huge-objects b-tree record set cannot coexist with `async`
    // (CS1988) either; the record set is per file and per address like every other structure, so it
    // lives in NativeCache instead (see HugeObjectsFractalHeapIdSubType1).
    //
    // `func` is async too, so a caller need not bridge the decode it performs. Note that the
    // callbacks in the tree ignore the driver handed to them and read from their own context - see the
    // remark in TinyObjectsFractalHeapIdSubType1.
    public abstract ValueTask<T> Read<T>(Func<H5DriverBase, ValueTask<T>> func);
}
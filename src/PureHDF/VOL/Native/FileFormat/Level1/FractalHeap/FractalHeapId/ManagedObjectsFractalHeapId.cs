namespace PureHDF.VOL.Native;

// Holds the context rather than a bare driver, now that FractalHeapHeader.GetAddress needs one. Safe
// to capture: this is constructed per heap-ID read inside a single operation (FractalHeapId.Construct)
// and discarded, so it never outlives the driver it holds.
internal sealed record class ManagedObjectsFractalHeapId(
    NativeReadContext Context,
    FractalHeapHeader Header,
    ulong Offset,
    ulong Length
) : FractalHeapId
{
    public static async ValueTask<ManagedObjectsFractalHeapId> Decode(
        NativeReadContext context,
        H5DriverBase localDriver,
        FractalHeapHeader header,
        ulong offsetByteCount,
        ulong lengthByteCount)
    {
        return new ManagedObjectsFractalHeapId(
            Context: context,
            Header: header,
            Offset: await ReadUtils.ReadUlong(localDriver, offsetByteCount).ConfigureAwait(false),
            Length: await ReadUtils.ReadUlong(localDriver, lengthByteCount).ConfigureAwait(false)
        );
    }

    public override async ValueTask<T> Read<T>(Func<H5DriverBase, ValueTask<T>> func)
    {
        // Locating a managed object walks the heap's indirect blocks, so this is a read in its own
        // right - which is why a synchronous Read<T> blocked every dense attribute and dense link
        // lookup regardless of how the layers above it awaited.
        var address = await Header.GetAddress(Context, this).ConfigureAwait(false);

        Context.Driver.SeekRelativeToBaseAddress((long)address);

        return await func(Context.Driver).ConfigureAwait(false);
    }
}
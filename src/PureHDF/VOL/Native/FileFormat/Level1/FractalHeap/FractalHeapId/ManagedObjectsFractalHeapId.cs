using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class ManagedObjectsFractalHeapId(
    H5DriverBase Driver,
    FractalHeapHeader Header,
    ulong Offset,
    ulong Length
) : FractalHeapId
{
    public static async ValueTask<ManagedObjectsFractalHeapId> Decode(
        H5DriverBase driver,
        H5DriverBase localDriver,
        FractalHeapHeader header,
        ulong offsetByteCount,
        ulong lengthByteCount)
    {
        return new ManagedObjectsFractalHeapId(
            Driver: driver,
            Header: header,
            Offset: await ReadUtils.ReadUlong(localDriver, offsetByteCount).ConfigureAwait(false),
            Length: await ReadUtils.ReadUlong(localDriver, lengthByteCount).ConfigureAwait(false)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        // NOTE (async propagation): FractalHeapHeader.GetAddress (out of scope,
        // FractalHeapHeader.cs) is now async, but this override must match the
        // abstract, synchronous `FractalHeapId.Read<T>` (out of scope), whose
        // `ref List<BTree2Record01> record01Cache` parameter can never itself become
        // async (CS1988 — the same constraint noted for BTree1Node.FoundDelegate).
        // Bridged synchronously here, mirroring the precedent already established in
        // NativeObject.EnumerateAttributeMessagesFromAttributeInfoMessage (out of scope).
        var address = Header.GetAddress(this).GetAwaiter().GetResult();

        Driver.SeekRelativeToBaseAddress((long)address);
        return func(Driver);
    }
}
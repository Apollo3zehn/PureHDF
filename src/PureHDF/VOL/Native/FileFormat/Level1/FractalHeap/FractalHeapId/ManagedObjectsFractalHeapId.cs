using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class ManagedObjectsFractalHeapId(
    H5DriverBase Driver,
    FractalHeapHeader Header,
    ulong Offset,
    ulong Length
) : FractalHeapId
{
    public static ManagedObjectsFractalHeapId Decode(
        H5DriverBase driver, 
        H5DriverBase localDriver, 
        FractalHeapHeader header, 
        ulong offsetByteCount, 
        ulong lengthByteCount)
    {
        return new ManagedObjectsFractalHeapId(
            Driver: driver,
            Header: header,
            Offset: ReadUtils.ReadUlong(localDriver, offsetByteCount),
            Length: ReadUtils.ReadUlong(localDriver, lengthByteCount)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func, 
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        var address = Header.GetAddress(this);

        Driver.Seek((long)address, SeekOrigin.Begin);
        return func(Driver);
    }
}
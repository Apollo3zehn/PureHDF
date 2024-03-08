using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal record class TinyObjectsFractalHeapIdSubType1(
    byte[] Data
) : FractalHeapId
{
    public static TinyObjectsFractalHeapIdSubType1 Decode(
        H5DriverBase localDriver,
        byte firstByte)
    {
        var length = (byte)(((firstByte & 0x0F) >> 0) + 1);

        return new TinyObjectsFractalHeapIdSubType1(
            Data: localDriver.ReadBytes(length)
        );
    }

    public override T Read<T>(
        Func<H5DriverBase, T> func,
        [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        using var driver = new H5StreamDriver(new MemoryStream(Data), leaveOpen: false);
        return func.Invoke(driver);
    }
}
namespace PureHDF.VOL.Native;

internal record class TinyObjectsFractalHeapIdSubType2(
    byte[] Data
) : TinyObjectsFractalHeapIdSubType1(Data)
{
    public static new async ValueTask<TinyObjectsFractalHeapIdSubType2> Decode(H5DriverBase localDriver, byte firstByte)
    {
        // extendedLength
        var extendedLength = await localDriver.ReadByte().ConfigureAwait(false);

        var highByte = (byte)((firstByte & 0x0F) >> 0);
        var length = (ushort)(extendedLength | (highByte << 8) + 1);

        return new TinyObjectsFractalHeapIdSubType2(
            Data: await localDriver.ReadBytes(length).ConfigureAwait(false)
        );
    }
}
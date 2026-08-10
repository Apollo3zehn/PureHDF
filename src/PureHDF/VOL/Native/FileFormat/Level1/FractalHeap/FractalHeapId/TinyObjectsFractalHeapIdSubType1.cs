namespace PureHDF.VOL.Native;

internal record class TinyObjectsFractalHeapIdSubType1(
    byte[] Data
) : FractalHeapId
{
    public static async ValueTask<TinyObjectsFractalHeapIdSubType1> Decode(
        H5DriverBase localDriver,
        byte firstByte)
    {
        var length = (byte)(((firstByte & 0x0F) >> 0) + 1);

        return new TinyObjectsFractalHeapIdSubType1(
            Data: await localDriver.ReadBytes(length).ConfigureAwait(false)
        );
    }

    // PRE-EXISTING DEFECT (unchanged here, and present identically in upstream v2.1.4): the driver
    // built below wraps the tiny object's inline bytes, but every callback in the tree ignores its
    // parameter and decodes from its own NativeReadContext instead - so a tiny heap ID decodes from
    // wherever the file cursor happens to sit rather than from Data. Fixing it means giving the
    // callback a context whose driver is this one, and there is no test file in the repo with tiny
    // link or attribute heap IDs to verify against, so it is recorded rather than guessed at.
    //
    // `using var` still outlives the await: the driver is disposed when this method returns, which is
    // after func has completed.
    public override async ValueTask<T> Read<T>(Func<H5DriverBase, ValueTask<T>> func)
    {
        using var driver = new H5StreamDriver(new MemoryStream(Data), leaveOpen: false);
        return await func.Invoke(driver).ConfigureAwait(false);
    }
}
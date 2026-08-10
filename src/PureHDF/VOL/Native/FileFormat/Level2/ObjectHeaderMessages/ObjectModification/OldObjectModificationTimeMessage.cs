namespace PureHDF.VOL.Native;

internal record class OldObjectModificationTimeMessage(
    int Year,
    int Month,
    int DayOfMonth,
    int Hour,
    int Minute,
    int Second
) : Message
{
    public static async ValueTask<OldObjectModificationTimeMessage> Decode(H5DriverBase driver)
    {
        // date / time
        var year = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 4).ConfigureAwait(false));
        var month = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 2).ConfigureAwait(false));
        var dayOfMonth = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 2).ConfigureAwait(false));
        var hour = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 2).ConfigureAwait(false));
        var minute = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 2).ConfigureAwait(false));
        var second = int.Parse(await ReadUtils.ReadFixedLengthString(driver, 2).ConfigureAwait(false));

        // reserved
        await driver.ReadBytes(2).ConfigureAwait(false);

        return new OldObjectModificationTimeMessage(
            Year: year,
            Month: month,
            DayOfMonth: dayOfMonth,
            Hour: hour,
            Minute: minute,
            Second: second
        );
    }

    public ObjectModificationMessage ToObjectModificationMessage()
    {
        var dateTime = new DateTime(Year, Month, DayOfMonth, Hour, Minute, Second);
        var secondsAfterUnixEpoch = (uint)((DateTimeOffset)dateTime).ToUnixTimeSeconds();

        return new(secondsAfterUnixEpoch)
        {
            Version = 1
        };
    }
}
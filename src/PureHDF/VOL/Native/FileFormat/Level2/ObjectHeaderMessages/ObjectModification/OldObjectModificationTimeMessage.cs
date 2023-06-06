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
    public static OldObjectModificationTimeMessage Decode(H5DriverBase driver)
    {
        // date / time
        var year = int.Parse(ReadUtils.ReadFixedLengthString(driver, 4));
        var month = int.Parse(ReadUtils.ReadFixedLengthString(driver, 2));
        var dayOfMonth = int.Parse(ReadUtils.ReadFixedLengthString(driver, 2));
        var hour = int.Parse(ReadUtils.ReadFixedLengthString(driver, 2));
        var minute = int.Parse(ReadUtils.ReadFixedLengthString(driver, 2));
        var second = int.Parse(ReadUtils.ReadFixedLengthString(driver, 2));

        // reserved
        driver.ReadBytes(2);

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
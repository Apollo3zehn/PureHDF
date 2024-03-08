namespace PureHDF.VOL.Native;

internal record class BogusMessage(
//
) : Message
{
    private uint _bogusValue;

    public required uint BogusValue
    {
        get
        {
            return _bogusValue;
        }
        init
        {
            if (value.ToString("X") != "deadbeef")
                throw new FormatException($"The bogus value of the {nameof(BogusMessage)} instance is invalid.");

            _bogusValue = value;
        }
    }

    public static BogusMessage Decode(H5DriverBase driver)
    {
        return new BogusMessage()
        {
            BogusValue = driver.ReadUInt32()
        };
    }
}
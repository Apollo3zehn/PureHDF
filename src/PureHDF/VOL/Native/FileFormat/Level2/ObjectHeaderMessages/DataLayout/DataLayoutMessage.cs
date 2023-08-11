namespace PureHDF.VOL.Native;

internal abstract record class DataLayoutMessage(
    LayoutClass LayoutClass)
    : Message
{
    public static DataLayoutMessage Construct(NativeReadContext context)
    {
        // get version
        var version = context.Driver.ReadByte();

        return version switch
        {
            >= 1 and < 3 => DataLayoutMessage12.Decode(context, version),
            3 => DataLayoutMessage3.Decode(context, version),
            4 => DataLayoutMessage4.Decode(context, version),
            _ => throw new NotSupportedException($"The data layout message version '{version}' is not supported.")
        };
    }
}
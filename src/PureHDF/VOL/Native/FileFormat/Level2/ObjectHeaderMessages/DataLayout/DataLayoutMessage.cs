namespace PureHDF.VOL.Native;

internal abstract record class DataLayoutMessage(
    LayoutClass LayoutClass)
    : Message
{
    public static async ValueTask<DataLayoutMessage> Construct(NativeReadContext context)
    {
        // get version
        var version = await context.Driver.ReadByte().ConfigureAwait(false);

        return version switch
        {
            >= 1 and < 3 => await DataLayoutMessage12.Decode(context, version).ConfigureAwait(false),
            3 => await DataLayoutMessage3.Decode(context, version).ConfigureAwait(false),
            4 => await DataLayoutMessage4.Decode(context, version).ConfigureAwait(false),
            _ => throw new NotSupportedException($"The data layout message version '{version}' is not supported.")
        };
    }
}
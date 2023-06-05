namespace PureHDF.VOL.Native;

internal abstract class DataLayoutMessage : Message
{
    #region Constructors

    public DataLayoutMessage()
    {
        //
    }

    #endregion

    #region Properties

    public LayoutClass LayoutClass { get; set; }

    public ulong Address { get; set; }

    #endregion

    #region Methods

    public static DataLayoutMessage Construct(NativeContext context)
    {
        // get version
        var version = context.Driver.ReadByte();

        return version switch
        {
            1 => new DataLayoutMessage12(context, version),
            2 => new DataLayoutMessage12(context, version),
            3 => new DataLayoutMessage3(context, version),
            4 => new DataLayoutMessage4(context, version),
            _ => throw new NotSupportedException($"The data layout message version '{version}' is not supported.")
        };
    }

    #endregion
}
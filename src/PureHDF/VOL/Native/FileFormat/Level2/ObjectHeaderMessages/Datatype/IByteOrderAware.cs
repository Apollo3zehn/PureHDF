namespace PureHDF.VOL.Native;

internal interface IByteOrderAware
{
    ByteOrder ByteOrder { get; init; }
}
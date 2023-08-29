namespace PureHDF.VOL.Native;

internal delegate void DecodeDelegate<T>(IH5ReadStream source, Memory<T> target);
internal delegate object? ElementDecodeDelegate(IH5ReadStream source);
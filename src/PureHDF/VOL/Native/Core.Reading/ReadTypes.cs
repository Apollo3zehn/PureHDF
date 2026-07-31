namespace PureHDF.VOL.Native;

internal delegate void DecodeDelegate<T>(IH5ReadStream source, Span<T> target);
internal delegate object? ElementDecodeDelegate(IH5ReadStream source);

internal delegate object? ElementDecodeDelegateBuffered(IH5ReadStream source, Span<byte> buffer);

internal readonly record struct DecodeStep(
    Action<object, object?>? SetValue,
    ulong CompoundMemberOffset,
    ElementDecodeDelegate ElementDecode
);
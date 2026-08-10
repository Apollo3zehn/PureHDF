namespace PureHDF.VOL.Native;

// NOTE (async-first): these two delegates sit in the innermost decode loop. They had to become
// ValueTask-returning (and Memory<T> rather than Span<T>, which cannot cross an await) so that a
// remote/streamed source can be read without blocking. For local sources every await completes
// synchronously, so no allocation occurs. This is the hot path shared by the #161/#162/#163 fast
// paths — changes here must preserve them exactly.
internal delegate ValueTask DecodeDelegate<T>(IH5ReadStream source, Memory<T> target);
internal delegate ValueTask<object?> ElementDecodeDelegate(IH5ReadStream source);

internal readonly record struct DecodeStep(
    Action<object, object?>? SetValue,
    ulong CompoundMemberOffset,
    ElementDecodeDelegate ElementDecode
);

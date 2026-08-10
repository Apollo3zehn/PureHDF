namespace PureHDF.VOL.Native;

// NOTE (async-first): these two delegates sit in the innermost decode loop. They had to become
// ValueTask-returning (and Memory<T> rather than Span<T>, which cannot cross an await) so that a
// remote/streamed source can be read without blocking. For local sources every await completes
// synchronously, so no allocation occurs. This is the hot path shared by the #161/#162/#163 fast
// paths — changes here must preserve them exactly.
//
// NOTE (context per call): NativeReadContext is a parameter here rather than something the
// cached delegate closes over. DatatypeMessage.GetDecodeInfo<TElement> caches the built delegate
// keyed on (Type, isRawMode) only, so the same cached delegate is reused across every read
// operation on that DatatypeMessage - including a planned per-operation driver allocation, where
// each operation gets its own NativeReadContext. A closure that captured the context from the
// first build would silently keep reading through the first operation's (possibly stale/reused)
// driver on every later call.
internal delegate ValueTask DecodeDelegate<T>(NativeReadContext context, IH5ReadStream source, Memory<T> target);
internal delegate ValueTask<object?> ElementDecodeDelegate(NativeReadContext context, IH5ReadStream source);

internal readonly record struct DecodeStep(
    Action<object, object?>? SetValue,
    ulong CompoundMemberOffset,
    ElementDecodeDelegate ElementDecode
);

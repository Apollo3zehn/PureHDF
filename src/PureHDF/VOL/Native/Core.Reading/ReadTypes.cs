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

// NOTE (context per call): decodes one b-tree key or record through the CALLER's context, for the
// same reason as the two delegates above. This replaced `Func<ValueTask<T>>`, which every call site
// built by currying a context into a closure - `() => DecodeGroupKey(context)`. That closure is
// precisely what stopped a decoded b-tree from being cacheable: the tree held the delegate, the
// delegate held a context, and the context holds a per-operation driver that is handed back to
// NativeOperationSlot and reused. With the context as a parameter, every call site can instead pass a
// static method group, which the compiler caches - so this also removes a closure allocation per
// navigation call.
internal delegate ValueTask<T> DecodeKeyDelegate<T>(NativeReadContext context);

internal readonly record struct DecodeStep(
    Action<object, object?>? SetValue,
    ulong CompoundMemberOffset,
    ElementDecodeDelegate ElementDecode
);

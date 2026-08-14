namespace PureHDF.VOL.Native;

// These two delegates sit in the innermost decode loop. They return ValueTask (and take Memory<T>
// rather than Span<T>, which cannot cross an await) so that a remote/streamed source can be read
// without blocking. For local sources every await completes synchronously, so no allocation occurs.
// This is the hot path shared by the #161/#162/#163 fast paths - changes here must preserve them
// exactly.
//
// NativeReadContext is a parameter rather than something the cached delegate closes over.
// DatatypeMessage.GetDecodeInfo<TElement> caches the built delegate keyed on (Type, isRawMode) only,
// so the same cached delegate is reused across every read operation on that DatatypeMessage - and
// each operation has its own NativeReadContext holding its own driver. A closure that captured the
// context from the first build would keep reading through that first operation's reused driver on
// every later call.
internal delegate ValueTask DecodeDelegate<T>(NativeReadContext context, IH5ReadStream source, Memory<T> target);
internal delegate ValueTask<object?> ElementDecodeDelegate(NativeReadContext context, IH5ReadStream source);

// Decodes one b-tree key or record through the CALLER's context, for the same reason as the two
// delegates above. Taking the context as a parameter rather than currying it into a closure is what
// makes a decoded b-tree cacheable: a closure would leave the tree holding the delegate, the delegate
// holding a context, and the context holding a per-operation driver that is handed back to
// NativeOperationSlot and reused. It also lets every call site pass a static method group, which the
// compiler caches, so there is no closure allocation per navigation call.
internal delegate ValueTask<T> DecodeKeyDelegate<T>(NativeReadContext context);

internal readonly record struct DecodeStep(
    Action<object, object?>? SetValue,
    ulong CompoundMemberOffset,
    ElementDecodeDelegate ElementDecode
);

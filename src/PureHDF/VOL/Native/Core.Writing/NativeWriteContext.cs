namespace PureHDF.VOL.Native;

/// <summary>
/// Identifies a cached datatype message. A <see cref="Type"/> alone is not enough: a string's
/// width and an opaque type's size and tag are supplied by the caller, so one type maps to many
/// messages. Everything that varies is part of the key, which is what makes the cache safe to
/// read for those types rather than having to skip them.
/// </summary>
/// <param name="Type">The .NET type being encoded.</param>
/// <param name="StringLength">The requested fixed-width string length; 0 means variable-length.</param>
/// <param name="StringPadding">
/// How a fixed-width string declares its unused bytes. Part of the key because two callers can ask
/// for the same width and mean different things by it: a width measured from a value fills the field
/// exactly and pads with nothing, while a declared width leaves room to terminate. Leaving it out
/// would let whichever is encoded first hand its padding to the other.
/// </param>
/// <param name="OpaqueInfo">The opaque size and tag, when encoding an opaque type.</param>
/// <param name="MemberStringLengths">The widths an object's own member length gives a compound's string members.</param>
internal readonly record struct DatatypeCacheKey(
    Type Type,
    int StringLength,
    PaddingType StringPadding,
    H5OpaqueInfo? OpaqueInfo,
    MemberStringLengths MemberStringLengths);

/// <summary>
/// The widths <see cref="H5StringInfo.MemberLength"/> gives the members of a compound type, indexed by member
/// with null where it gives none. Compared by content, because each object brings its own delegate.
/// </summary>
internal readonly struct MemberStringLengths(int?[] lengths) : IEquatable<MemberStringLengths>
{
    private readonly int?[]? _lengths = lengths;

    public int? this[int index] => _lengths?[index];

    public bool Equals(MemberStringLengths other)
    {
        return (_lengths ?? []).SequenceEqual(other._lengths ?? []);
    }

    public override bool Equals(object? obj)
    {
        return obj is MemberStringLengths other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        foreach (var length in _lengths ?? [])
        {
            hashCode.Add(length);
        }

        return hashCode.ToHashCode();
    }
}

internal record NativeWriteContext(
    H5NativeWriter Writer,
    H5File File,
    H5DriverBase Driver,
    FreeSpaceManager FreeSpaceManager,
    GlobalHeapManager GlobalHeapManager,
    H5WriteOptions WriteOptions,
    Dictionary<H5Dataset, (H5D_Base H5D, object Encode)> DatasetToInfoMap,
    Dictionary<DatasetInfo, (long ObjectHeaderStart, int ObjectHeaderLength)> DatasetInfoToObjectHeaderMap,
    Dictionary<DatatypeCacheKey, (DatatypeMessage, ElementEncodeDelegate)> TypeToMessageMap,
    Dictionary<H5Object, ulong> ObjectToAddressMap,
    Dictionary<H5Object, int> ObjectReferenceCountMap,
    Dictionary<object, H5Dataset> RawValueToDatasetMap,
    SystemMemoryStream ShortlivedStream
);
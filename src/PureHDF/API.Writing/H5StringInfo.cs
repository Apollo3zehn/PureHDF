using System.Reflection;

namespace PureHDF;

/// <summary>
///     How the strings of one dataset or attribute are stored. Takes precedence over the file-wide string
///     options of <see cref="H5WriteOptions" />.
/// </summary>
/// <param name="Length">
///     The width in bytes of the object's strings, in place of <see cref="H5WriteOptions.DefaultStringLength" />:
///     zero for variable-length strings, a positive value for fixed-length strings of that many bytes, or
///     <see langword="null" /> to keep the file-wide setting. For an attribute it also takes the place of
///     <see cref="H5WriteOptions.AttributeStringLength" />. A string member of a compound type takes this width
///     when neither <paramref name="MemberLength" /> nor the string length mappers answer for it.
/// </param>
/// <param name="Padding">
///     What fills the unused bytes of the object's fixed-length strings, compound members included.
///     <see langword="null" /> keeps what the writer would otherwise declare.
/// </param>
/// <param name="MemberLength">
///     Maps a string field or property of the object's compound type to its width in bytes, consulted before
///     <see cref="H5WriteOptions.FieldStringLengthMapper" /> and <see cref="H5WriteOptions.PropertyStringLengthMapper" />.
///     Return <see langword="null" /> to leave a member to them. It applies to the members of the object's
///     own element type; a compound nested inside it is sized by the string length mappers and
///     <paramref name="Length" />.
/// </param>
public record H5StringInfo(
    int? Length = default,
    H5StringPadding? Padding = default,
    Func<MemberInfo, int?>? MemberLength = default)
{
    private readonly int? _length = ValidateLength(Length);

    /// <summary>
    ///     The width in bytes of the object's strings: zero for variable-length, or <see langword="null" /> to
    ///     keep the file-wide setting.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The length is negative.</exception>
    public int? Length
    {
        get => _length;
        init => _length = ValidateLength(value);
    }

    private static int? ValidateLength(int? length)
    {
        return length is null or >= 0
            ? length
            : throw new ArgumentOutOfRangeException(nameof(Length), length, "A string length must not be negative.");
    }
}

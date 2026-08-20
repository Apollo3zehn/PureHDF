namespace PureHDF;

/// <summary>
///     How the writer sizes a string attribute.
/// </summary>
/// <remarks>
///     A complete answer for attributes rather than a modifier on <see cref="H5WriteOptions.DefaultStringLength" />,
///     so that the two can disagree: variable-length datasets alongside measured attributes, or a declared
///     width for compound members alongside variable-length attributes.
///     <para>
///         Applies to an attribute whose elements ARE strings. A string that is a MEMBER of a compound attribute is
///         sized by <see cref="H5WriteOptions.DefaultStringLength" /> or a string length mapper regardless of this
///         setting, because a member's width has to stay uniform across every object sharing that type.
///     </para>
/// </remarks>
public enum H5AttributeStringLength
{
    /// <summary>
    ///     Fixed-length, as wide as the attribute's own longest value in UTF-8 bytes. The default.
    /// </summary>
    /// <remarks>
    ///     A measured width cannot truncate the value and pads nothing, which is why it is the default:
    ///     <see cref="H5WriteOptions.DefaultStringLength" /> is file-global, so one width has to serve the
    ///     widest attribute anywhere in the file and every narrower attribute pays the difference in padding.
    ///     <para>
    ///         An attribute holding a null element stays variable-length, since that is the one value a
    ///         fixed-length field cannot represent - so measuring never loses anything.
    ///     </para>
    /// </remarks>
    Measured = 0,

    /// <summary>
    ///     Whatever <see cref="H5WriteOptions.DefaultStringLength" /> says - fixed-length at that width, or
    ///     variable-length when it is 0.
    /// </summary>
    /// <remarks>
    ///     A value wider than the declared width is subject to <see cref="H5WriteOptions.StringOverflow" />.
    /// </remarks>
    Inherit = 1,

    /// <summary>
    ///     Variable-length, whatever <see cref="H5WriteOptions.DefaultStringLength" /> says.
    /// </summary>
    /// <remarks>
    ///     The value lives in a global heap collection rather than in the attribute, which is what allows
    ///     another tool to replace it with a longer one in place.
    /// </remarks>
    VariableLength = 2
}
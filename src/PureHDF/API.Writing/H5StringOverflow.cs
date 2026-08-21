namespace PureHDF;

/// <summary>
///     What the writer does with a string too long for the fixed-length width it is being written into.
/// </summary>
/// <remarks>
///     Only reachable where a width is DECLARED rather than measured - a compound member sized by
///     <see cref="H5WriteOptions.DefaultStringLength" /> or one of the string length mappers, or an attribute
///     set to <see cref="H5AttributeStringLength.Inherit" />. A measured width is taken from the value itself,
///     so it cannot overflow.
///     <para>
///         Note that an HDF5 string width is in BYTES, not characters. A width chosen by counting characters is
///         too small for any value outside ASCII, and it is the multi-byte cases that overflow first.
///     </para>
/// </remarks>
public enum H5StringOverflow
{
    /// <summary>
    ///     Keep the leading bytes that fit and discard the rest, silently. The default, because a declared
    ///     width is a statement that values conform to it.
    /// </summary>
    /// <remarks>
    ///     Truncation is by BYTE, so it can cut a multi-byte character in half and leave a sequence that is
    ///     not valid UTF-8 - the loss is then not confined to the tail. Prefer <see cref="Throw" /> where
    ///     silent loss is worse than a failed write.
    /// </remarks>
    Truncate = 0,

    /// <summary>
    ///     Fail the write instead of discarding data.
    /// </summary>
    Throw = 1
}
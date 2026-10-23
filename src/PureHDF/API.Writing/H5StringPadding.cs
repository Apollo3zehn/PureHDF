namespace PureHDF;

/// <summary>
///     What fills the unused bytes of a fixed-length string, and so what a reader strips from its end.
/// </summary>
public enum H5StringPadding
{
    /// <summary>
    ///     The value ends at the first null byte; the rest of the field is filled with nulls.
    /// </summary>
    NullTerminate = 0,

    /// <summary>
    ///     The field is filled with nulls after the value, and a value may use the full width.
    /// </summary>
    NullPad = 1,

    /// <summary>
    ///     The field is filled with spaces after the value. A reader strips trailing spaces, so a value's own
    ///     trailing spaces do not survive.
    /// </summary>
    SpacePad = 2
}

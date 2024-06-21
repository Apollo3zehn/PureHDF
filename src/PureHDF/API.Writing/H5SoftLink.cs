namespace PureHDF;

/// <summary>
/// A soft link.
/// </summary>
/// <param name="Target">The link target path.</param>
public record H5SoftLink(string Target);
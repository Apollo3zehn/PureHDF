namespace PureHDF;

/// <summary>
/// Specifies the fixed-length string length.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class H5StringLengthAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5StringLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The desired fixed-length string length.</param>
    public H5StringLengthAttribute(int length)
    {
        Length = length;
    }

    /// <summary>
    /// Gets the name of the member.
    /// </summary>
    public int Length { get; set; }
}
namespace PureHDF;

/// <summary>
/// Specifies the member name that is present in the HDF5 compound data type.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class H5NameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5NameAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the member.</param>
    public H5NameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the name of the member.
    /// </summary>
    public string Name { get; set; }
}
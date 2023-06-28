namespace PureHDF.Experimental;

/// <summary>
/// An H5 file.
/// </summary>
/// <param name="Attributes">The attributes that belong to the top-level group.</param>
/// <param name="Objects">The objects that belong to the top-level group.</param>
public record class H5File(
    IReadOnlyList<H5AttributeBase> Attributes,
    IReadOnlyList<H5Object> Objects)
    : H5Group("/", Attributes, Objects)
{
    /// <summary>
    /// Creates a new file, write the contents to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The name of the file.</param>
    public void Save(string filePath)
    {
        H5Writer.Serialize(this, filePath);
    }
};

/// <summary>
/// A group.
/// </summary>
/// <param name="Name">The group name.</param>
/// <param name="Attributes">The attributes that belong to this group.</param>
/// <param name="Objects">The objects that belong to this group.</param>
public record class H5Group(
    string Name,
    IReadOnlyList<H5AttributeBase>? Attributes = default,
    IReadOnlyList<H5Object>? Objects = default)
    : H5AttributableObject(Name, Attributes);

/// <summary>
/// A dataset.
/// </summary>
/// <typeparam name="T">The type of data this dataset represents.</typeparam>
/// <param name="Name">The dataset name.</param>
/// <param name="Attributes">The attributes that belong to this dataset.</param>
public record class H5Dataset<T>(
    string Name,
    IReadOnlyList<H5AttributeBase>? Attributes = default)
    : H5AttributableObject(Name, Attributes);

/// <summary>
/// A base class for attributable objects.
/// </summary>
/// <param name="Name">The object name.</param>
/// <param name="Attributes">The attributes that belong to this object.</param>
public record class H5AttributableObject(
    string Name,
    IReadOnlyList<H5AttributeBase>? Attributes)
    : H5Object(Name);

/// <summary>
/// A base class for objects.
/// </summary>
/// <param name="Name">The object name.</param>
public record class H5Object(
    string Name);

/// <summary>
/// A generic attribute.
/// </summary>
/// <typeparam name="T">The type of data this attribute represents.</typeparam>
/// <param name="Name">The attribute name.</param>
/// <param name="data">The attribute data.</param>
public record class H5Attribute<T>(
    string Name,
    T[] data)
    : H5AttributeBase(Name) where T : unmanaged;

/// <summary>
/// Non-generic base class for attributes.
/// </summary>
/// <param name="Name">The attribute name.</param>
public abstract record class H5AttributeBase(
    string Name);
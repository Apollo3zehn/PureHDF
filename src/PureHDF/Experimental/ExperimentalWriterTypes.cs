using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PureHDF.Experimental;

/// <summary>
/// Provides options to be used with <see cref="H5File"/>.
/// </summary>
/// <param name="DefaultStringLength">Gets a value that indicates how strings are handled during serialization. A nonzero positve value means that strings are treated as fixed-length strings, otherwise they are variable-length strings. The default value is 0.</param>
/// <param name="GlobalHeapCollectionSize">Gets a value that indicates the size of a global heap collection in bytes. The default value is 4096 bytes which is the minimum allowed size.</param>
/// <param name="GlobalHeapFlushThreshold">Gets a value that indicates the threshold after which global heap collections will be flushed. The default value is 4096 * 1024 = 4 MB.</param>
/// <param name="IncludeStructFields">Gets a value that indicates whether struct fields are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="IncludeStructProperties">Gets a value that indicates whether struct properties are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassFields">Gets a value that indicates whether class fields are handled during serialization. The default value is <see langword="false"/>.</param>
/// <param name="IncludeClassProperties">Gets a value that indicates whether class properties are handled during serialization. The default value is <see langword="true"/>.</param>
/// <param name="FieldNameMapper">Maps a <see cref="FieldInfo"/> to the name of the HDF5 member.</param>
/// <param name="FieldStringLengthMapper">Maps a <see cref="FieldInfo"/> of type string to the desired string length.</param>
/// <param name="PropertyNameMapper">Maps a <see cref="PropertyInfo"/> to the name of the HDF5 member.</param>
/// <param name="PropertyStringLengthMapper">Maps a <see cref="PropertyInfo"/> of type string to the desired string length.</param>
public record H5SerializerOptions(
    int DefaultStringLength = default,
    int GlobalHeapCollectionSize = 4096,
    long GlobalHeapFlushThreshold = 4096 * 1024,
    bool IncludeStructFields = true,
    bool IncludeStructProperties = false,
    bool IncludeClassFields = false,
    bool IncludeClassProperties = true,
    Func<FieldInfo, string?>? FieldNameMapper = default,
    Func<FieldInfo, int?>? FieldStringLengthMapper = default,
    Func<PropertyInfo, string?>? PropertyNameMapper = default,
    Func<PropertyInfo, int?>? PropertyStringLengthMapper = default
);

/// <summary>
/// An H5 file.
/// </summary>
public class H5File : H5Group
{
    /// <summary>
    /// Creates a new file, write the contents to the file, and then closes the file. If the target file already exists, it is overwritten.
    /// </summary>
    /// <param name="filePath">The name of the file.</param>
    /// <param name="options">Options to control serialization behavior.</param>
    public void Save(string filePath, H5SerializerOptions? options = default)
    {
        H5Writer.Serialize(this, filePath, options ?? new H5SerializerOptions());
    }
};

/// <summary>
/// A group.
/// </summary>
public class H5Group : H5AttributableObject, IDictionary<string, H5Object>
{
    private readonly IDictionary<string, H5Object> _objects = new Dictionary<string, H5Object>();

    /// <inheritdoc />
    public H5Object this[string key] 
    { 
        get => _objects[key];
        set => _objects[key] = value;
    }

    #region IDictionary

    /// <inheritdoc />
    public ICollection<string> Keys => _objects.Keys;

    /// <inheritdoc />
    public ICollection<H5Object> Values => _objects.Values;

    /// <inheritdoc />
    public int Count => _objects.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _objects.IsReadOnly;

    /// <inheritdoc />
    public void Add(string key, H5Object value)
    {
        _objects.Add(key, value);
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<string, H5Object> item)
    {
        _objects.Add(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _objects.Clear();
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, H5Object> item)
    {
        return _objects.Contains(item);
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _objects.ContainsKey(key);
    }

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, H5Object>[] array, int arrayIndex)
    {
        _objects.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, H5Object>> GetEnumerator()
    {
        return _objects.GetEnumerator();
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        return _objects.Remove(key);
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, H5Object> item)
    {
        return _objects.Remove(item);
    }

#if NETSTANDARD2_0 || NETSTANDARD2_1
    /// <inheritdoc />
    public bool TryGetValue(string key, out H5Object value)
    {
        return _objects.TryGetValue(key, out value);
    }
#else
    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out H5Object value)
    {
        return _objects.TryGetValue(key, out value);
    }
#endif

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _objects.GetEnumerator();
    }

    #endregion
}

/// <summary>
/// A dataset.
/// </summary>
/// <typeparam name="T">The type of data this dataset represents.</typeparam>
public class H5Dataset<T> : H5AttributableObject
{
    //
}

/// <summary>
/// A base class for attributable objects.
/// </summary>
public class H5AttributableObject : H5Object
{
    /// <summary>
    /// A map of attributes that belong to this object.
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>
/// A base class for HDF5 objects.
/// </summary>
public class H5Object
{
    //
}

/// <summary>
/// An attribute.
/// </summary>
public class H5Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5Attribute"/> class.
    /// </summary>
    /// <param name="data">The attribute data.</param>
    /// <param name="dimensions">The attribute dimensions.</param>
    public H5Attribute(
        object data,
        ulong[]? dimensions = default)
    {
        Data = data;
        Dimensions = dimensions;
    }

    internal object Data { get; init; }
    internal ulong[]? Dimensions { get; init; }
}
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF;

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
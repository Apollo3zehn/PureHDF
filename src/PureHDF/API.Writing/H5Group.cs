using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF;

/// <summary>
/// A group.
/// </summary>
public class H5Group : H5Object, IDictionary<string, object>
{
    private readonly IDictionary<string, object> _objects = new Dictionary<string, object>();

    /// <inheritdoc />
    public object this[string key]
    {
        get => _objects[key];
        set => _objects[key] = value;
    }

    #region IDictionary

    /// <inheritdoc />
    public ICollection<string> Keys => _objects.Keys;

    /// <inheritdoc />
    public ICollection<object> Values => _objects.Values;

    /// <inheritdoc />
    public int Count => _objects.Count;

    /// <inheritdoc />
    public bool IsReadOnly => _objects.IsReadOnly;

    /// <inheritdoc />
    public void Add(string key, object value)
    {
        _objects.Add(key, value);
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<string, object> item)
    {
        _objects.Add(item);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _objects.Clear();
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, object> item)
    {
        return _objects.Contains(item);
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return _objects.ContainsKey(key);
    }

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        _objects.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return _objects.GetEnumerator();
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        return _objects.Remove(key);
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, object> item)
    {
        return _objects.Remove(item);
    }

#if NETSTANDARD2_0 || NETSTANDARD2_1
    /// <inheritdoc />
    public bool TryGetValue(string key, out object value)
    {
        return _objects.TryGetValue(key, out value);
    }
#else
    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
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
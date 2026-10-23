using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF;

/// <summary>
/// A group.
/// </summary>
public class H5Group : H5Object, IDictionary<string, object>
{
    // Most groups hold a handful of links, and a dictionary for each - its table, its buckets, the
    // object itself - is about 380 bytes for a group of five, most of a file's tree when it has many
    // small groups. Up to this many links are kept in an array of pairs instead, searched in order, and
    // the group moves them into a dictionary once it holds more.
    private const int MAXIMUM_ARRAY_COUNT = 8;

    private KeyValuePair<string, object>[]? _entries;
    private int _count;
    private Dictionary<string, object>? _dictionary;

    // Changed by every modification, so that enumerating a group while it is changed fails as it does for
    // a dictionary rather than skipping or repeating links.
    private int _version;

    /// <inheritdoc />
    public object this[string key]
    {
        get => TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"The given key '{key}' was not present in the group.");

        set => Set(key, value, add: false);
    }

    #region IDictionary

    /// <inheritdoc />
    /// <remarks>
    /// A copy while the group is small, rather than a view that follows later changes.
    /// </remarks>
    public ICollection<string> Keys => _dictionary is not null
        ? _dictionary.Keys
        : CopyOut(static entry => entry.Key);

    /// <inheritdoc />
    /// <remarks>
    /// A copy while the group is small, rather than a view that follows later changes.
    /// </remarks>
    public ICollection<object> Values => _dictionary is not null
        ? _dictionary.Values
        : CopyOut(static entry => entry.Value);

    /// <inheritdoc />
    public int Count => _dictionary?.Count ?? _count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(string key, object value)
    {
        Set(key, value, add: true);
    }

    /// <inheritdoc />
    public void Add(KeyValuePair<string, object> item)
    {
        Set(item.Key, item.Value, add: true);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _entries = default;
        _count = 0;
        _dictionary = default;
        _version++;
    }

    /// <inheritdoc />
    public bool Contains(KeyValuePair<string, object> item)
    {
        return TryGetValue(item.Key, out var value) &&
            EqualityComparer<object>.Default.Equals(value, item.Value);
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        return TryGetValue(key, out _);
    }

    /// <inheritdoc />
    public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
    {
        if (_dictionary is not null)
        {
            ((ICollection<KeyValuePair<string, object>>)_dictionary).CopyTo(array, arrayIndex);
            return;
        }

        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < _count)
            throw new ArgumentException("The destination array is too small.", nameof(array));

        Entries().CopyTo(array.AsSpan(arrayIndex));
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return _dictionary is not null
            ? _dictionary.GetEnumerator()
            : EnumerateEntries();
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        if (_dictionary is not null)
        {
            if (!_dictionary.Remove(key))
                return false;

            _version++;
            return true;
        }

        var index = IndexOf(key);

        if (index < 0)
            return false;

        // Shifted rather than swapped with the last, so that the links keep the order they were added in.
        Array.Copy(_entries!, index + 1, _entries!, index, _count - index - 1);
        _entries![--_count] = default;
        _version++;

        return true;
    }

    /// <inheritdoc />
    public bool Remove(KeyValuePair<string, object> item)
    {
        return Contains(item) && Remove(item.Key);
    }

    /// <inheritdoc />
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
    {
        if (_dictionary is not null)
            return _dictionary.TryGetValue(key, out value);

        var index = IndexOf(key);

        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _entries![index].Value;
        return true;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion

    private ReadOnlySpan<KeyValuePair<string, object>> Entries()
        => _entries.AsSpan(0, _count);

    private T[] CopyOut<T>(Func<KeyValuePair<string, object>, T> select)
    {
        var result = new T[_count];

        for (var i = 0; i < _count; i++)
            result[i] = select(_entries![i]);

        return result;
    }

    private int IndexOf(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (var i = 0; i < _count; i++)
        {
            if (string.Equals(_entries![i].Key, key, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private void Set(string key, object value, bool add)
    {
        if (_dictionary is not null)
        {
            if (add)
                _dictionary.Add(key, value);

            else
                _dictionary[key] = value;

            _version++;
            return;
        }

        var index = IndexOf(key);

        if (index >= 0)
        {
            if (add)
                throw new ArgumentException($"An item with the same key has already been added. Key: {key}", nameof(key));

            _entries![index] = new(key, value);
            _version++;
            return;
        }

        if (_count == MAXIMUM_ARRAY_COUNT)
        {
            _dictionary = new Dictionary<string, object>(_count + 1);

            foreach (var entry in Entries())
                _dictionary.Add(entry.Key, entry.Value);

            _dictionary.Add(key, value);
            _entries = default;
            _count = 0;
            _version++;
            return;
        }

        if (_entries is null || _entries.Length == _count)
            Array.Resize(ref _entries, _entries is null ? 4 : _entries.Length * 2);

        _entries[_count++] = new(key, value);
        _version++;
    }

    private IEnumerator<KeyValuePair<string, object>> EnumerateEntries()
    {
        var version = _version;

        for (var i = 0; i < _count; i++)
        {
            yield return _entries![i];

            if (version != _version)
                throw new InvalidOperationException("The group was modified; enumeration operation may not execute.");
        }
    }
}

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace PureHDF;

/// <summary>
///     A group of an <see cref="H5StreamingWriter" /> that is still taking links. Its header, with its
///     attributes, is written when it is closed - by <see cref="Close" />, by disposing it, by closing a group
///     above it, or when the writer is disposed.
/// </summary>
/// <remarks>
///     Once the group is closed, its attributes can still be read, but changing them, appending to the group or
///     creating a group below it throws an <see cref="InvalidOperationException" />.
/// </remarks>
public sealed class H5OpenGroup : IDisposable
{
    private readonly ClosableAttributes _attributes;
    private readonly H5StreamingWriter _writer;

    internal H5OpenGroup(H5StreamingWriter writer, string path)
    {
        _writer = writer;
        _attributes = new ClosableAttributes(this);
        Path = path;
    }

    /// <summary>
    ///     The path of the group, relative to the root group.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     A map of attributes that belong to this group. It can be changed until the group is closed.
    /// </summary>
    public IDictionary<string, object> Attributes => _attributes;

    internal bool IsClosed { get; private set; }

    /// <summary>
    ///     Closes this group, as <see cref="Close" /> does.
    /// </summary>
    public void Dispose()
    {
        Close();
    }

    /// <summary>
    ///     Writes <paramref name="subtree" /> and links it at <paramref name="path" /> below this group, as
    ///     <see cref="H5StreamingWriter.Append" /> does.
    /// </summary>
    /// <param name="path">The path of the new link, relative to this group, its segments separated by '/'.</param>
    /// <param name="subtree">The group or dataset to write.</param>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     This group has been closed, or <see cref="H5StreamingWriter.Append" /> rejects the path or the subtree.
    /// </exception>
    public void Append(string path, H5Object subtree)
    {
        ThrowIfClosed();
        _writer.Append(Combine(path), subtree);
    }

    /// <summary>
    ///     The open group at <paramref name="path" /> below this group, created if it does not exist, as
    ///     <see cref="H5StreamingWriter.Group(string)" /> returns it.
    /// </summary>
    /// <param name="path">The path of the group, relative to this group, its segments separated by '/'.</param>
    /// <returns>The open group.</returns>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     This group has been closed, or the path leads through a closed group or a dataset, or names one.
    /// </exception>
    public H5OpenGroup Group(string path)
    {
        ThrowIfClosed();
        return _writer.Group(Combine(path));
    }

    /// <summary>
    ///     The open group at <paramref name="path" /> below this group, as
    ///     <see cref="H5StreamingWriter.Group(string, IEnumerable{KeyValuePair{string, object}})" /> returns it.
    /// </summary>
    /// <param name="path">The path of the group, relative to this group, its segments separated by '/'.</param>
    /// <param name="attributes">The attributes to add: copied, and overwriting those the group has already.</param>
    /// <returns>The open group.</returns>
    /// <exception cref="ArgumentException">The path is empty or has an empty segment.</exception>
    /// <exception cref="InvalidOperationException">
    ///     This group has been closed, or the path leads through a closed group or a dataset, or names one.
    /// </exception>
    public H5OpenGroup Group(string path, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        ThrowIfClosed();
        return _writer.Group(Combine(path), attributes);
    }

    /// <summary>
    ///     Closes this group and every open group below it, and writes their headers. Closing a closed group
    ///     does nothing.
    /// </summary>
    public void Close()
    {
        if (!IsClosed)
            _writer.Close(Path);
    }

    /// <summary>
    ///     Marks the group closed, and returns the attributes to write into its header.
    /// </summary>
    internal Dictionary<string, object> MarkClosed()
    {
        IsClosed = true;
        return _attributes.Inner;
    }

    private string Combine(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return $"{Path}/{path}";
    }

    private void ThrowIfClosed()
    {
        if (IsClosed)
            throw new InvalidOperationException($"The group '{Path}' has been closed.");
    }

    /// <summary>
    ///     A map of attributes that can be read at any time, but changed only while its group is open.
    /// </summary>
    private sealed class ClosableAttributes(H5OpenGroup group) : IDictionary<string, object>
    {
        public Dictionary<string, object> Inner { get; } = new();

        public object this[string key]
        {
            get => Inner[key];

            set
            {
                group.ThrowIfClosed();
                Inner[key] = value;
            }
        }

        public ICollection<string> Keys => Inner.Keys;

        public ICollection<object> Values => Inner.Values;

        public int Count => Inner.Count;

        public bool IsReadOnly => group.IsClosed;

        public void Add(string key, object value)
        {
            group.ThrowIfClosed();
            Inner.Add(key, value);
        }

        public void Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(string key)
        {
            group.ThrowIfClosed();
            return Inner.Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            group.ThrowIfClosed();
            return ((ICollection<KeyValuePair<string, object>>)Inner).Remove(item);
        }

        public void Clear()
        {
            group.ThrowIfClosed();
            Inner.Clear();
        }

        public bool ContainsKey(string key)
        {
            return Inner.ContainsKey(key);
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)Inner).Contains(item);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            return Inner.TryGetValue(key, out value);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)Inner).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return Inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
namespace PureHDF;

/// <summary>
/// A base class for HDF5 objects.
/// </summary>
public class H5Object
{
    private int? _hardLinkCount;

    internal Dictionary<string, object>? InternalAttributes { get; set; }

    /// <summary>
    /// How many hard links to this object the file will have, or null to count them from the tree.
    /// </summary>
    /// <remarks>
    /// An object's header carries its hard link count, so the count has to be known when the header is
    /// written. <see cref="H5File.BeginWrite(Stream, H5WriteOptions?)" /> counts the links in the tree,
    /// but <see cref="H5File.BeginStreaming(Stream, H5WriteOptions?)" /> writes each appended subtree
    /// before it sees the next: an object linked from more than one appended subtree has to declare its
    /// count before it is first appended. Every link is checked against a declared count, and the
    /// writer throws an <see cref="InvalidOperationException" /> when there are more links than
    /// declared, or fewer once the tree is complete.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int? HardLinkCount
    {
        get => _hardLinkCount;

        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), value, "An object has at least one hard link.");

            _hardLinkCount = value;
        }
    }

    /// <summary>
    /// A map of attributes that belong to this object.
    /// </summary>
    public Dictionary<string, object> Attributes
    {
        get
        {
            if (InternalAttributes is null)
                InternalAttributes = new();

            return InternalAttributes;
        }

        set
        {
            InternalAttributes = value;
        }
    }
}
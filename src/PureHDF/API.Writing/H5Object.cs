namespace PureHDF;

/// <summary>
/// A base class for HDF5 objects.
/// </summary>
public class H5Object
{
    internal Dictionary<string, object>? InternalAttributes { get; set; }

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
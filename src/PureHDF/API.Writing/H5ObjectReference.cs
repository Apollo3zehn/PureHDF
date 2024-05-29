namespace PureHDF;

/// <summary>
/// An object reference.
/// </summary>
public class H5ObjectReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="H5ObjectReference"/> class.
    /// </summary>
    public H5ObjectReference(
        H5Object referencedObject)
    {
        ReferencedObject = referencedObject;
    }

    internal H5Object ReferencedObject { get; }

    /// <summary>
    /// Converts the referenced object into an object reference.
    /// </summary>
    /// <param name="referencedObject">The referenced object.</param>
    public static implicit operator H5ObjectReference(H5Object referencedObject) 
        => new H5ObjectReference(referencedObject);
}
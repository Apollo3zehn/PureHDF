namespace PureHDF.VOL;

internal class H5AttributableObject : H5Object, IH5AttributableObject
{
    public H5AttributableObject(string name) : base(name)
    {
        //
    }

    public IEnumerable<IH5Attribute> Attributes { get => throw new NotImplementedException(); }

    public IH5Attribute Attribute(string name) => throw new NotImplementedException();

    public bool AttributeExists(string name) => throw new NotImplementedException();
}
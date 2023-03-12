namespace PureHDF.VOL.Hsds;

internal class H5Object : IH5Object
{
    public H5Object(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
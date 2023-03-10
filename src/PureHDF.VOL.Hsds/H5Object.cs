namespace PureHDF.VOL;

internal class H5Object : IH5Object
{
    public H5Object(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
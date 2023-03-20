namespace PureHDF.VOL.Hsds;

internal class HsdsObject : IH5Object
{
    public HsdsObject(string name, string id)
    {
        Name = name;
        Id = id;
    }

    public string Name { get; }

    public string Id { get; }
}
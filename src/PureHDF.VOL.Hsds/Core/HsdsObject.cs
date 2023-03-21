namespace PureHDF.VOL.Hsds;

internal class HsdsObject : IH5Object
{
    public HsdsObject(HsdsNamedReference reference)
    {
        Reference = reference;
    }

    public string Name => Reference.Title;

    public string Id => Reference.Id;

    internal HsdsNamedReference Reference { get; set; }
}
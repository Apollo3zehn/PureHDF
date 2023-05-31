using PureHDF.VOL.Hsds;

namespace PureHDF;

internal struct HsdsNamedReference
{
    #region Constructors

    public HsdsNamedReference(string collection, string title, string id, InternalHsdsConnector connector)
    {
        Collection = collection;
        Title = title;
        Id = id;
        Connector = connector;
    }

    #endregion

    #region Properties

    public string Collection { get; set; }

    public string Title { get; set; }

    public string Id { get; }

    public InternalHsdsConnector Connector { get; }

    #endregion

    #region Methods

    public readonly HsdsObject Dereference()
    {
        return Collection switch
        {
            "groups" => new HsdsGroup(Connector, this),
            "datasets" => new HsdsDataset(Connector, this),
            // https://github.com/HDFGroup/hdf-rest-api/blob/e6f1a685c34ce4db68cdbdbcacacd053176a0136/openapi.yaml#L804-L805
            _ => throw new Exception($"The link collection type {Collection} is not supported. Please contact the library maintainer to enable support for this type of collection.")
        };
    }

    #endregion
}
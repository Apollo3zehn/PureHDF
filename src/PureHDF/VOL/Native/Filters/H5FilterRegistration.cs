namespace PureHDF.Filters;

internal class H5FilterRegistration
{
    public H5FilterRegistration(
        FilterIdentifier identifier, 
        string name, 
        Func<FilterInfo, Memory<byte>> filterFunction)
    {
        Identifier = identifier;
        Name = name;
        FilterFunction = filterFunction;
    }

    public FilterIdentifier Identifier { get; set; }
    //public bool HasEncoder { get; set; }
    //public bool HasDecoder { get; set; }
    public string Name { get; set; }
    //public Func<H5Dataset, bool> CanApply { get; set; }
    //public Action<H5Dataset> SetLocal { get; set; }
    public Func<FilterInfo, Memory<byte>> FilterFunction { get; set; }
}

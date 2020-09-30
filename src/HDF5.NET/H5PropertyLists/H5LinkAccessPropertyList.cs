namespace HDF5.NET
{
    public struct H5LinkAccessPropertyList
    {
        public bool KeepSymbolicLinks { get; set; }
        public string ExternalFilePrefix { get; set; }
    }
}

namespace HDF5.NET
{
    public abstract class Selection
    {
        public abstract ulong TotalElementCount { get; }

        public abstract IEnumerable<Step> Walk(ulong[] limits);
    }
}
namespace HDF5.NET
{
    partial class DelegateSelection : Selection
    {
        private readonly Func<ulong[], IEnumerable<Step>> _walker;
    }
}
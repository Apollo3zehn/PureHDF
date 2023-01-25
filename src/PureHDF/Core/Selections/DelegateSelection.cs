namespace PureHDF
{
    partial class DelegateSelection : Selection
    {
        private readonly Func<ulong[], IEnumerable<Step>> _walker;
    }
}
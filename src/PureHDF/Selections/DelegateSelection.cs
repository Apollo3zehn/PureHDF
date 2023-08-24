namespace PureHDF.Selections;

/// <summary>
/// A selection which uses a delegate to get the information about how to walk through the data.
/// </summary>
public class DelegateSelection : Selection
{
    private readonly Func<ulong[], IEnumerable<Step>> _walker;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateSelection"/> class.
    /// </summary>
    /// <param name="totalElementCount">The total number of elements which is used to preallocate the returned buffer.</param>
    /// <param name="walker">The user-defined walker delegate.</param>
    public DelegateSelection(ulong totalElementCount, Func<ulong[], IEnumerable<Step>> walker)
    {
        TotalElementCount = totalElementCount;
        _walker = walker;
    }

    /// <inheritdoc />
    public override ulong TotalElementCount { get; }

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        return _walker(limits);
    }
}
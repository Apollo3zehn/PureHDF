namespace PureHDF.Selections;

/// <summary>
/// This selection selects no elements in the dataset.
/// </summary>
public class NoneSelection : Selection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoneSelection"/> instance.
    /// </summary>
    public NoneSelection()
    {
        //
    }

    /// <inheritdoc />
    public override ulong TotalElementCount => 0;

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        yield break;
    }
}
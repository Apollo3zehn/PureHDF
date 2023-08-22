namespace PureHDF;

/// <summary>
/// This selection selects all elements in the dataset.
/// </summary>
public class AllSelection : Selection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AllSelection"/> instance.
    /// </summary>
    public AllSelection()
    {
        //
    }

    /// <inheritdoc />
    public override ulong TotalElementCount => throw new Exception("The total element count cannot be determined.");

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        throw new Exception("The walk method cannot be called on this selection.");
    }
}
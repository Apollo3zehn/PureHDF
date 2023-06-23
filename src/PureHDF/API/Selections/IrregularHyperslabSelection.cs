namespace PureHDF;

/// <summary>
/// An irregular hyperslab is a selection of elements from a hyper rectangle.
/// </summary>
public class IrregularHyperslabSelection : Selection
{
    private readonly IrregularHyperslabSelectionInfo _info;

    internal IrregularHyperslabSelection(IrregularHyperslabSelectionInfo info)
    {
        _info = info;
    }

    /// <inheritdoc />
    public override ulong TotalElementCount => 4;

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        var rank = _info.Rank;
        var step = new Step() { Coordinates = new ulong[rank] };

        for (int blockIndex = 0; blockIndex < (int)_info.BlockCount; blockIndex++)
        {
            for (int dimension = 0; dimension < rank; dimension++)
            {
                step.Coordinates[dimension] = _info.BlockOffsets[blockIndex * rank * 2 + dimension];
                step.ElementCount = 2;
            }

            yield return step;
        }
    }
}
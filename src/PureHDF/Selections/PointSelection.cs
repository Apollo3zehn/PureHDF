namespace PureHDF.Selections;

/// <summary>
/// A selection which uses a collection of points to select the data.
/// </summary>
public partial class PointSelection : Selection
{
    internal readonly ulong[,] PointsField;

    /// <summary>
    /// Initializes a new instance of the <see cref="PointSelection"/> class.
    /// </summary>
    /// <param name="points">The points to be selected.</param>
    public PointSelection(ulong[,] points)
    {
        PointsField = points;
        TotalElementCount = (ulong)points.GetLength(0);
    }

    /// <inheritdoc />
    public override ulong TotalElementCount { get; }

    /// <inheritdoc />
    public override IEnumerable<Step> Walk(ulong[] limits)
    {
        var rank = PointsField.GetLength(1);
        var coordinates = new ulong[rank];

        for (ulong i = 0; i < TotalElementCount; i++)
        {
            for (int j = 0; j < rank; j++)
            {
                coordinates[j] = PointsField[i, j];
            }

            var step = new Step()
            {
                Coordinates = coordinates,
                ElementCount = 1
            };

            yield return step;
        }
    }
}
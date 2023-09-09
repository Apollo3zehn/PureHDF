namespace PureHDF.Selections;

/// <summary>
/// Represents a single unit of data to be selected.
/// </summary>
/// <param name="Coordinates">The data coordinates.</param>
/// <param name="ElementCount">The number of elements to select along the fastest changing dimension.</param>
public readonly record struct Step(ulong[] Coordinates, ulong ElementCount);
internal readonly record struct LinearIndexResult(bool Success, ulong LinearIndex, ulong MaxCount);
internal readonly record struct CoordinatesResult(ulong[] Coordinates, ulong MaxCount);
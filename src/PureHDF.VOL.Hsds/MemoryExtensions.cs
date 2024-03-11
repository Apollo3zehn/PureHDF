namespace PureHDF;

internal static class MemoryExtensions
{
    public static Memory<TTo> Cast<TFrom, TTo>(this Memory<TFrom> from)
        where TFrom : struct
        where TTo : struct
    {
        // avoid the extra allocation/indirection, at the cost of a gen-0 box
        if (typeof(TFrom) == typeof(TTo))
            return (Memory<TTo>)(object)from;

        return new CastMemoryManager<TFrom, TTo>(from).Memory;
    }
}
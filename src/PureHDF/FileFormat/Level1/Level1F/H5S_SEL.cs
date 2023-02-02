namespace PureHDF
{
    internal abstract class H5S_SEL
    {
        public H5S_SEL()
        {
            //
        }

        public abstract LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates);
        public abstract CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex);
    }
}

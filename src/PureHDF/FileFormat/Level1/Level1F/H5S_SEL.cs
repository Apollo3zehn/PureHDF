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

        public static ulong ReadEncodedValue(H5BaseReader reader, byte encodeSize)
        {
            return encodeSize switch
            {
                2 => reader.ReadUInt16(),
                4 => reader.ReadUInt32(),
                8 => reader.ReadUInt64(),
                _ => throw new Exception($"Invalid encode size {encodeSize}.")
            };
        }
    }
}

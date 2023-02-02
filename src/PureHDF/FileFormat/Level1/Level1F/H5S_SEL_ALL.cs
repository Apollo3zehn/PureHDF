namespace PureHDF
{
    internal class H5S_SEL_ALL : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_ALL(H5BaseReader reader)
        {
            // version
            Version = reader.ReadUInt32();

            // reserved
            reader.ReadBytes(8);
        }

        #endregion

        #region Properties

        public uint Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_ALL)} are supported.");

                _version = value;
            }
        }

        public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
        {
            var linearIndex = H5Utils.ToLinearIndex(coordinates, sourceDimensions);
            var maxCount = sourceDimensions[^1] - coordinates[^1];

            return new LinearIndexResult(
                Success: true, // TODO theoretically this can fail
                linearIndex,
                maxCount);
        }

        public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
        {
            var coordinates = H5Utils.ToCoordinates(linearIndex, sourceDimensions);
            var maxCount = sourceDimensions[^1] - coordinates[^1];

            return new CoordinatesResult(
                Success: true, // TODO theoretically this can fail
                coordinates, 
                maxCount);
        }

        #endregion
    }
}

namespace PureHDF
{
    internal class H5S_SEL_ALL : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_ALL(H5DriverBase driver)
        {
            // version
            Version = driver.ReadUInt32();

            // reserved
            driver.ReadBytes(8);
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
            var linearIndex = Utils.ToLinearIndex(coordinates, sourceDimensions);
            var maxCount = sourceDimensions[^1] - coordinates[^1];

            return new LinearIndexResult(
                Success: true, // TODO theoretically this can fail
                linearIndex,
                maxCount);
        }

        public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
        {
            var coordinates = Utils.ToCoordinates(linearIndex, sourceDimensions);
            var maxCount = sourceDimensions[^1] - coordinates[^1];

            // TODO theoretically this can fail
            return new CoordinatesResult(
                coordinates, 
                maxCount);
        }

        #endregion
    }
}

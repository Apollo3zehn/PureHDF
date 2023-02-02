namespace PureHDF
{
    internal class H5S_SEL_NONE : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_NONE(H5BaseReader reader)
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
                    throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_NONE)} are supported.");

                _version = value;
            }
        }

        public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
        {
            return default;
        }

        public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
        {
            return default;
        }

        #endregion
    }
}

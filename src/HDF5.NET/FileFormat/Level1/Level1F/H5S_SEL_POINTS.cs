namespace HDF5.NET
{
    internal class H5S_SEL_POINTS : H5S_SEL
    {
        #region Fields

        private uint _version;

        #endregion

        #region Constructors

        public H5S_SEL_POINTS(H5BaseReader reader)
        {
            // version
            Version = reader.ReadUInt32();

            // reserved
            reader.ReadBytes(4);

            // length
            var length = reader.ReadUInt32();

            // rank
            Rank = reader.ReadUInt32();

            // point count
            PointCount = reader.ReadUInt32();

            // point data
            PointData = new uint[Rank * PointCount];

            for (int i = 0; i < (length - 8) / 4; i++)
            {
                PointData[i] = reader.ReadUInt32();
            }
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
                    throw new FormatException($"Only version 1 instances of type {nameof(H5S_SEL_POINTS)} are supported.");

                _version = value;
            }
        }

        public uint Rank { get; set; }
        public uint PointCount { get; set; }
        public uint[] PointData { get; set; }

        #endregion
    }
}

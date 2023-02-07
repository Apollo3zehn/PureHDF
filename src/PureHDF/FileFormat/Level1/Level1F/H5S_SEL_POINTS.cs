namespace PureHDF
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

            // encode size
            byte encodeSize;

            switch (Version)
            {
                case 1:
                    // encode size
                    encodeSize = 4;

                    // reserved
                    _ = reader.ReadBytes(4);

                    // length
                    _ = reader.ReadUInt32();

                    break;

                case 2:
                    // encode size
                    encodeSize = reader.ReadByte();

                    break;
                
                default:
                    throw new NotSupportedException($"Only {nameof(H5S_SEL_POINTS)} of version 1 or 2 are supported.");
            }

            // rank
            Rank = reader.ReadUInt32();

            // point count
            var pointCount = ReadEncodedValue(reader, encodeSize);

            // point data
            PointData = new ulong[pointCount, Rank];

            for (ulong pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    PointData[pointIndex, dimension] = ReadEncodedValue(reader, encodeSize);
                }
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
                if (!(1 <= value && value <= 2))
                    throw new FormatException($"Only version 1 and version 2 instances of type {nameof(H5S_SEL_POINTS)} are supported.");

                _version = value;
            }
        }

        public uint Rank { get; set; }
        public ulong[,] PointData { get; set; }

        public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
        {
            var result = default(LinearIndexResult);

            for (ulong pointIndex = 0; pointIndex < (ulong)PointData.Length; pointIndex++)
            {
                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    var requestedCoordinate = coordinates[dimension];
                    var currentCoordinate = PointData[pointIndex, dimension];

                    if (currentCoordinate == requestedCoordinate)
                    {
                        if (dimension == Rank - 1)
                        {
                            return new LinearIndexResult(
                                Success: true, 
                                LinearIndex: pointIndex,
                                MaxCount: 1);
                        }
                    }

                    else
                    {
                        if (dimension == Rank - 1 && currentCoordinate < requestedCoordinate)
                        {
                            var maxCount = requestedCoordinate - currentCoordinate;

                            if (result.MaxCount == 0 || maxCount < result.MaxCount)
                            {
                                result = new LinearIndexResult(
                                    Success: false, 
                                    LinearIndex: default, 
                                    MaxCount: maxCount);
                            }
                        }

                        break;
                    }
                }
            }

            return result;
        }

        public override CoordinatesResult ToCoordinates(ulong[] sourceDimensions, ulong linearIndex)
        {
            if (linearIndex < (ulong)PointData.Length)
            {
                var coordinates = new ulong[Rank];

                for (int dimension = 0; dimension < Rank; dimension++)
                {
                    coordinates[dimension] = PointData[(long)linearIndex, dimension];
                }

                return new CoordinatesResult(Coordinates: coordinates, MaxCount: 1);
            }

            else
            {
                throw new Exception("This should never happen.");
            }
        }

        #endregion
    }
}

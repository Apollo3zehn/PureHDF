namespace PureHDF.VOL.Native;

internal record class H5S_SEL_POINTS(
    uint Rank,
    ulong[,] PointData
) : H5S_SEL
{
    private uint _version;

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

    public static H5S_SEL_POINTS Decode(H5DriverBase driver)
    {
        // version
        var version = driver.ReadUInt32();

        // encode size
        byte encodeSize;

        switch (version)
        {
            case 1:
                // encode size
                encodeSize = 4;

                // reserved
                _ = driver.ReadBytes(4);

                // length
                _ = driver.ReadUInt32();

                break;

            case 2:
                // encode size
                encodeSize = driver.ReadByte();

                break;

            default:
                throw new NotSupportedException($"Only {nameof(H5S_SEL_POINTS)} of version 1 or 2 are supported.");
        }

        // rank
        var rank = driver.ReadUInt32();

        // point count
        var pointCount = ReadEncodedValue(driver, encodeSize);

        // point data
        var pointData = new ulong[pointCount, rank];

        for (ulong pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            for (int dimension = 0; dimension < rank; dimension++)
            {
                pointData[pointIndex, dimension] = ReadEncodedValue(driver, encodeSize);
            }
        }

        return new H5S_SEL_POINTS(
            Rank: rank,
            PointData: pointData
        )
        {
            Version = version
        };
    }

    public override LinearIndexResult ToLinearIndex(ulong[] sourceDimensions, ulong[] coordinates)
    {
        var result = default(LinearIndexResult);
        var pointCount = PointData.GetLength(0);

        for (ulong pointIndex = 0; pointIndex < (ulong)pointCount; pointIndex++)
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
                    if (dimension == Rank - 1 && requestedCoordinate < currentCoordinate)
                    {
                        var maxCount = currentCoordinate - requestedCoordinate;

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
}
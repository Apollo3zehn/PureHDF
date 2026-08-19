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

    public static async ValueTask<H5S_SEL_POINTS> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadUInt32().ConfigureAwait(false);

        // encode size
        byte encodeSize;

        switch (version)
        {
            case 1:
                // encode size
                encodeSize = 4;

                // reserved
                _ = await driver.ReadBytes(4).ConfigureAwait(false);

                // length
                _ = await driver.ReadUInt32().ConfigureAwait(false);

                break;

            case 2:
                // encode size
                encodeSize = await driver.ReadByte().ConfigureAwait(false);

                break;

            default:
                throw new NotSupportedException($"Only {nameof(H5S_SEL_POINTS)} of version 1 or 2 are supported.");
        }

        // rank
        var rank = await driver.ReadUInt32().ConfigureAwait(false);

        // point count
        var pointCount = await ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);

        // point data
        var pointData = new ulong[pointCount, rank];

        for (ulong pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            for (int dimension = 0; dimension < rank; dimension++)
            {
                pointData[pointIndex, dimension] = await ReadEncodedValue(driver, encodeSize).ConfigureAwait(false);
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
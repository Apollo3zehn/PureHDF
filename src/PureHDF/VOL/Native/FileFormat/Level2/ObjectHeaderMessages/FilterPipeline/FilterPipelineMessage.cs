namespace PureHDF.VOL.Native;

internal record class FilterPipelineMessage(
    List<FilterDescription> FilterDescriptions
) : Message
{
    private byte _version;

    private byte _filterCount;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 2))
                throw new FormatException($"Only version 1 and 2 instances of type {nameof(FilterPipelineMessage)} are supported.");

            _version = value;
        }
    }

    public required byte FilterCount
    {
        get
        {
            return _filterCount;
        }
        init
        {
            if (value > 32)
                throw new FormatException($"An instance of type {nameof(FilterPipelineMessage)} can only contain a maximum of 32 filters.");

            _filterCount = value;
        }
    }

    public static FilterPipelineMessage Decode(NativeContext context)
    {
        var (driver, _) = context;

        // version
        var version = driver.ReadByte();

        // filter count
        var filterCount = driver.ReadByte();

        // reserved
        if (version == 1)
            driver.ReadBytes(6);

        // filter descriptions
        var filterDescriptions = new List<FilterDescription>(filterCount);

        for (int i = 0; i < filterCount; i++)
        {
            filterDescriptions.Add(FilterDescription.Decode(context, version));
        }

        return new FilterPipelineMessage(
            FilterDescriptions: filterDescriptions
        )
        {
            Version = version,
            FilterCount = filterCount
        };
    }
}
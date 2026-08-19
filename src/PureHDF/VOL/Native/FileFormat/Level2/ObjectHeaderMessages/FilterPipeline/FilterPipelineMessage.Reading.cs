namespace PureHDF.VOL.Native;

internal partial record class FilterPipelineMessage(
    FilterDescription[] FilterDescriptions
) : Message
{
    private byte _version;

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

    public static async ValueTask<FilterPipelineMessage> Decode(H5DriverBase driver)
    {
        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // filter count
        var filterCount = await driver.ReadByte().ConfigureAwait(false);

        if (filterCount > 32)
            throw new FormatException($"An instance of type {nameof(FilterPipelineMessage)} can only contain a maximum of 32 filters.");

        // reserved
        if (version == 1)
            await driver.ReadBytes(6).ConfigureAwait(false);

        // filter descriptions
        var filterDescriptions = new FilterDescription[filterCount];

        for (int i = 0; i < filterCount; i++)
        {
            filterDescriptions[i] = await FilterDescription.Decode(driver, version).ConfigureAwait(false);
        }

        return new FilterPipelineMessage(
            FilterDescriptions: filterDescriptions
        )
        {
            Version = version
        };
    }
}
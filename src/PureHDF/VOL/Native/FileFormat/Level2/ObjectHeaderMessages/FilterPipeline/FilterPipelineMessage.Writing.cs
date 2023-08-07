namespace PureHDF.VOL.Native;

internal partial record class FilterPipelineMessage
{
    public static FilterPipelineMessage Create(ICollection<H5FilterID> filterIds)
    {
        if (filterIds.Count > 32)
            throw new FormatException($"An instance of type {nameof(FilterPipelineMessage)} can only contain a maximum of 32 filters.");

        var filterDescriptions = filterIds.Select(filterId =>
        {
            if (!H5Filter.Registrations.TryGetValue((FilterIdentifier)filterId, out var registration))
                throw new Exception($"The filter with id {filterId} has not been registered.");

            return new FilterDescription(
                Identifier: registration.Identifier,
                Flags: FilterFlags.None,
                Name: registration.Name,
                ClientData: Array.Empty<uint>()
            );
        }).ToArray();

        return new FilterPipelineMessage(
            FilterDescriptions: filterDescriptions
        )
        {
            Version = 2
        };
    }

    public override ushort GetEncodeSize()
    {
        if (Version != 2)
            throw new Exception("Only version 2 filter pipeline messages are supported.");

        var size =
            sizeof(byte) +
            sizeof(byte) +
            FilterDescriptions.Aggregate(0, (sum, description) => sum + description.GetEncodeSize());
            
        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        if (Version != 2)
            throw new Exception("Only version 2 filter pipeline messages are supported.");

        // version
        driver.Write(Version);

        // number of filters
        driver.Write((byte)FilterDescriptions.Length);
        
        // filter descriptions
        foreach (var description in FilterDescriptions)
        {
            description.Encode(driver);
        }
    }
}
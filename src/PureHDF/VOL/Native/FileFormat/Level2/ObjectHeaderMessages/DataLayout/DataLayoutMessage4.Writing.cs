namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4
{
    public static DataLayoutMessage4 Create(
        WriteContext context,
        EncodeDelegate encode,
        ulong dataEncodeSize,
        object data,
        uint[]? chunkDimensions)
    {
        var preferCompact = context.SerializerOptions.PreferCompactDatasetLayout;
        var dataLayout = default(DataLayoutMessage4);

        if (chunkDimensions is not null)
        {
            // TODO: this works only for the implicit index
            var address = context.FreeSpaceManager.Allocate((long)dataEncodeSize);

            var properties = new ChunkedStoragePropertyDescription4(
                Address: (ulong)address,
                Rank: (byte)(chunkDimensions.Length + 1),
                Flags: default,
                DimensionSizes: chunkDimensions.Select(value => (ulong)value).ToArray(),
                IndexingTypeInformation: new ImplicitIndexingInformation()
            );

            dataLayout = new DataLayoutMessage4(
                LayoutClass: LayoutClass.Chunked,
                Address: default,
                Properties: properties
            )
            {
                Version = 4
            };
        }

        else
        {
            // TODO: The ushort.MaxValue limit is not stated in the specification but
            // makes sense because of the size field of the Compact Storage Property
            // Description.
            //
            // See also H5Dcompact.c (H5D__compact_construct): "Verify data size is 
            // smaller than maximum header message size (64KB) minus other layout 
            // message fields."

            /* try to create compact dataset */
            if (preferCompact && dataEncodeSize <= ushort.MaxValue)
            {
                var properties = new CompactStoragePropertyDescription(
                    InputData: default!,
                    EncodeData: driver => encode(driver.BaseStream, data),
                    EncodeDataSize: (ushort)dataEncodeSize
                );

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Compact,
                    Address: default,
                    Properties: properties
                )
                {
                    Version = 4
                };

                var dataLayoutEncodeSize = dataLayout.GetEncodeSize();

                if (dataEncodeSize + dataLayoutEncodeSize > ushort.MaxValue)
                    dataLayout = default;
            }

            /* create contiguous dataset */
            if (dataLayout == default)
            {
                var address = context.FreeSpaceManager.Allocate((long)dataEncodeSize);

                var properties = new ContiguousStoragePropertyDescription(
                    Address: (ulong)address,
                    Size: dataEncodeSize
                );

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Contiguous,
                    Address: default,
                    Properties: properties
                )
                {
                    Version = 4
                };
            }
        }

        return dataLayout;
    }

    public override ushort GetEncodeSize()
    {
        var size =
            sizeof(byte) +
            sizeof(byte) +
            Properties.GetEncodeSize();

        return (ushort)size;
    }

    public override void Encode(BinaryWriter driver)
    {
        // version
        driver.Write(Version);

        // layout class
        driver.Write((byte)LayoutClass);

        // properties
        Properties.Encode(driver);
    }
}
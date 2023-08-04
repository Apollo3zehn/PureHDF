namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4
{
    public static DataLayoutMessage4 Create<T>(
        NativeWriteContext context,
        EncodeDelegate<T> encode,
        ulong dataEncodeSize,
        Memory<T> data,
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
                // TODO avoid creation of system memory stream too often
                var buffer = new byte[dataEncodeSize];
                var localWriter = new SystemMemoryStream(buffer);

                var properties = new CompactStoragePropertyDescription(
                    InputData: default!,
                    EncodeData: driver => 
                    {
                        encode(data, localWriter);
                        driver.Write(buffer);
                    },
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

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // layout class
        driver.Write((byte)LayoutClass);

        // properties
        Properties.Encode(driver);
    }
}
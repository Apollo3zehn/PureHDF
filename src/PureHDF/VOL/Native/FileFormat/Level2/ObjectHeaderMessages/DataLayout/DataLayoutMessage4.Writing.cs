namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4
{
    public static DataLayoutMessage4 Create(
        NativeWriteContext context,
        uint typeSize,
        bool isFiltered,
        bool isDeferred,
        ulong[]? dataDimensions,
        uint[]? chunkDimensions)
    {
        var preferCompact = context.WriteOptions.PreferCompactDatasetLayout;
        var dataLayout = default(DataLayoutMessage4);

        if (dataDimensions is not null && chunkDimensions is not null)
        {
            var chunkCount = 1UL;

            for (int dimension = 0; dimension < chunkDimensions.Length; dimension++)
            {
                chunkCount *= (ulong)Math
                    .Ceiling(dataDimensions[dimension] / (double)chunkDimensions[dimension]);
            }

            (IndexingInformation IndexingInformation, long EncodeSize, ChunkedStoragePropertyFlags Flags) indexInfo;

            if (chunkCount == 1)
            {
                /* FilteredChunkSize and encode size will be set later, see also H5D__single_idx_insert (H5DSingle.c) */
                var indexingInformation = new SingleChunkIndexingInformation(ChunkFilters: default);

                var flags = isFiltered
                    ? ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER
                    : ChunkedStoragePropertyFlags.None;

                indexInfo = (
                    indexingInformation,
                    0,
                    flags);
            }

            else
            {
                indexInfo = isFiltered
                    ?
                        (
                            new FixedArrayIndexingInformation(
                                /* H5D__layout_set_latest_indexing (H5Dlayout.c) => H5D_FARRAY_MAX_DBLK_PAGE_NELMTS_BITS */
#if NETSTANDARD2_0 || NETSTANDARD2_1
                                PageBits: (byte)Math.Ceiling(Math.Log(chunkCount, newBase: 2))
#else
                                PageBits: (byte)Math.Ceiling(Math.Log2(chunkCount))
#endif
                            ),
                            FixedArrayHeader.ENCODE_SIZE,
                            ChunkedStoragePropertyFlags.None
                        )
                    :
                        (
                            new ImplicitIndexingInformation(),
                            (long)(
                                chunkCount * 
                                chunkDimensions.Aggregate(1UL, (product, dimension) => product * dimension) * 
                                typeSize
                            ),
                            ChunkedStoragePropertyFlags.None
                        );
            }

            /* some indexes can only be allocated later */
            var address = indexInfo.EncodeSize == 0
                ? Superblock.UndefinedAddress
                : (ulong)context.FreeSpaceManager.Allocate(indexInfo.EncodeSize);

            var properties = new ChunkedStoragePropertyDescription4(
                Rank: (byte)(chunkDimensions.Length + 1),
                Flags: indexInfo.Flags,

                DimensionSizes: chunkDimensions
                    .Select(value => (ulong)value)
                    .Concat(new ulong[] { typeSize })
                    .ToArray(),

                IndexingInformation: indexInfo.IndexingInformation
            )
            {
                Address = address
            };

            dataLayout = new DataLayoutMessage4(
                LayoutClass: LayoutClass.Chunked,
                Properties: properties
            )
            {
                Version = 4
            };
        }

        else
        {
            var dataBlockSize = dataDimensions is null
                ? 0
                : dataDimensions.Aggregate(1UL, (product, dimension) => product * dimension);

            var dataEncodeSize = (long)(dataBlockSize * typeSize);

            // TODO: The ushort.MaxValue limit is not stated in the specification but
            // makes sense because of the size field of the Compact Storage Property
            // Description.
            //
            // See also H5Dcompact.c (H5D__compact_construct): "Verify data size is 
            // smaller than maximum header message size (64KB) minus other layout 
            // message fields."

            /* try to create compact dataset */
            if (preferCompact && dataEncodeSize <= ushort.MaxValue && !isDeferred)
            {
                // TODO avoid creation of system memory stream too often
                var buffer = new byte[dataEncodeSize];
                var localWriter = new SystemMemoryStream(buffer);

                var properties = new CompactStoragePropertyDescription(
                    Data: dataEncodeSize == 0
                        ? Array.Empty<byte>()
                        : new byte[dataEncodeSize]
                )
                {
                    Address = Superblock.UndefinedAddress
                };

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Compact,
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
                var address = context.FreeSpaceManager.Allocate(dataEncodeSize);

                var properties = new ContiguousStoragePropertyDescription(
                    Size: (ulong)dataEncodeSize
                )
                {
                    Address = (ulong)address
                };

                dataLayout = new DataLayoutMessage4(
                    LayoutClass: LayoutClass.Contiguous,
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
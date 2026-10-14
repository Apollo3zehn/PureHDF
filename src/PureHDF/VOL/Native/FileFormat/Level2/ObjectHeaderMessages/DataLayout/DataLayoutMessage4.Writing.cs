namespace PureHDF.VOL.Native;

internal partial record class DataLayoutMessage4
{
    // allocatesPayload: whether to allocate a contiguous or implicitly indexed dataset's payload now. When
    // not, its address is left undefined and unallocatedPayloadSize says how much to allocate later. The
    // other layouts allocate their chunks as they are written anyway.
    public static DataLayoutMessage4 Create(
        NativeWriteContext context,
        uint typeSize,
        bool isFiltered,
        bool isDeferred,
        ulong[]? dataDimensions,
        uint[]? chunkDimensions,
        bool allocatesPayload,
        out long unallocatedPayloadSize,
        long variableLengthSize = 0)
    {
        var preferCompact = context.WriteOptions.PreferCompactDatasetLayout;
        var dataLayout = default(DataLayoutMessage4);

        unallocatedPayloadSize = 0;

        if (dataDimensions is not null && chunkDimensions is not null)
        {
            var chunkCount = 1UL;

            for (int dimension = 0; dimension < chunkDimensions.Length; dimension++)
            {
                chunkCount *= (ulong)Math
                    .Ceiling(dataDimensions[dimension] / (double)chunkDimensions[dimension]);
            }

            (IndexingInformation IndexingInformation, long EncodeSize, AllocationKind Kind, ChunkedStoragePropertyFlags Flags) indexInfo;

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
                    AllocationKind.Metadata,
                    flags);
            }

            else
            {
                /* H5D__layout_set_latest_indexing (H5Dlayout.c) => H5D_FARRAY_MAX_DBLK_PAGE_NELMTS_BITS */
                var pageBits = (byte)Math.Ceiling(Math.Log2(chunkCount));

                indexInfo = isFiltered
                    ?
                        (
                            new FixedArrayIndexingInformation(
                                PageBits: pageBits
                            ),

                            /* The header and its data block, together: the block's size follows from the
                             * chunk count, so it need not wait until the chunks are written. Allocated at
                             * Dispose, it would be structure that turns up after the structure has been
                             * placed. See H5D_Chunk4_FixedArray.Dispose.
                             */
                            FixedArrayHeader.ENCODE_SIZE + (long)GetFixedArrayDataBlockEncodeSize(
                                pageBits,
                                chunkCount,
                                chunkByteSize: chunkDimensions.Aggregate(1UL, (product, dimension) => product * dimension) * typeSize),

                            AllocationKind.Metadata,
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

                            /* An implicit index has no index structure at all: the chunks sit
                             * contiguously from this address, so what is allocated here is the whole
                             * payload rather than metadata. See H5D_Chunk4_Implicit, which derives a
                             * chunk's address by offsetting into it arithmetically.
                             */
                            AllocationKind.RawData,
                            ChunkedStoragePropertyFlags.None
                        );
            }

            /* some indexes can only be allocated later */
            var address = Superblock.UndefinedAddress;

            if (!allocatesPayload && indexInfo.IndexingInformation is ImplicitIndexingInformation)
                unallocatedPayloadSize = indexInfo.EncodeSize;

            // An unfiltered single chunk is stored at its full size, so it can be allocated on the first
            // write like contiguous data, and the header that names it completed then.
            else if (!allocatesPayload && !isFiltered && indexInfo.IndexingInformation is SingleChunkIndexingInformation)
                unallocatedPayloadSize = (long)(chunkDimensions.Aggregate(1UL, (product, dimension) => product * dimension) * typeSize);

            // A filtered dataset's index is written once its chunks are, and is allocated then too, with
            // the header that names it: allocated before, it is a gap until then. See H5D_Chunk4.AllocateIndex.
            else if (indexInfo.EncodeSize != 0 && (allocatesPayload || !isFiltered))
                address = (ulong)context.FreeSpaceManager.Allocate(indexInfo.EncodeSize, indexInfo.Kind);

            var properties = new ChunkedStoragePropertyDescription4(
                Rank: (byte)(chunkDimensions.Length + 1),
                Flags: indexInfo.Flags,

                DimensionSizes: chunkDimensions
                    .Select(value => (ulong)value)
                    .Concat([typeSize])
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

            // The whole layout message - version, layout class, a two-byte data size, then the data -
            // has to fit the two-byte size an object header message declares.
            //
            // See also H5Dcompact.c (H5D__compact_construct): "Verify data size is 
            // smaller than maximum header message size (64KB) minus other layout 
            // message fields."
            //
            // Within that, the caller's maximum, against which the variable-length values the data
            // names count too: they are placed with the header.
            var fitsCompact = FitsCompact(context, dataEncodeSize, variableLengthSize);

            /* try to create compact dataset */
            if (preferCompact && fitsCompact && !isDeferred)
            {
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
            }

            /* create contiguous dataset */
            if (dataLayout == default)
            {
                var address = Superblock.LongUndefinedAddress;

                if (allocatesPayload)
                    address = context.FreeSpaceManager.Allocate(dataEncodeSize, AllocationKind.RawData);

                else
                    unallocatedPayloadSize = dataEncodeSize;

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

    /// <summary>
    /// Whether data of <paramref name="dataEncodeSize" /> bytes, naming <paramref name="variableLengthSize" />
    /// bytes of variable-length values, can be written compact.
    /// </summary>
    public static bool FitsCompact(NativeWriteContext context, long dataEncodeSize, long variableLengthSize)
        => dataEncodeSize <= H5WriteOptions.MAXIMUM_COMPACT_DATASET_SIZE &&
            dataEncodeSize + variableLengthSize <= context.WriteOptions.MaximumCompactDatasetSize;

    internal static ulong GetFixedArrayDataBlockEncodeSize(byte pageBits, ulong chunkCount, ulong chunkByteSize)
    {
        /* H5FA__cache_hdr_serialize (H5FAcache.c) */
        var entrySize = FilteredDataBlockElement.GetEncodeSize(MathUtils.ComputeChunkSizeLength(chunkByteSize));
        var (_, pageCount, pageBitmapSize) = H5D_Chunk4_FixedArray.GetInfo(pageBits, chunkCount);

        return FixedArrayDataBlock<FilteredDataBlockElement>.GetEncodeSize(chunkCount, pageCount, pageBitmapSize, entrySize);
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
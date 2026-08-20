namespace PureHDF.VOL.Native;

internal class H5D_Chunk4_FixedArray : H5D_Chunk4
{
    /// <summary>
    /// Everything a repeated read needs to reuse below the header: the data block, where its pages
    /// begin, and the pages themselves.
    /// </summary>
    /// <remarks>
    /// One cached object rather than three, because the first page address is not recorded anywhere in
    /// the file - it is wherever the data block decode happened to leave the cursor - so it has to be
    /// captured together with the block that produced it.
    /// <para>
    /// The pages are bounded and the data block is not, which is the same split the b-tree headers use.
    /// A data block holds either a page bitmap (when paged) or every element (when not, and then the
    /// element count is at most one page by definition), so it is small either way. The PAGES are the
    /// part that grows with the chunk count, so they get the bound.
    /// </para>
    /// </remarks>
    private sealed record class CachedIndex<T>(
        FixedArrayDataBlock<T> DataBlock,
        long FirstPageAddress,
        BoundedAddressCache<DataBlockPage<T>> Pages
    ) where T : DataBlockElement;

    private FixedArrayHeader? _header;

    public H5D_Chunk4_FixedArray(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        DataLayoutMessage4 layout,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation):
        base(readContext, writeContext, dataset, layout, datasetAccess, datasetCreation)
    {
        //
    }

    protected override async ValueTask<ChunkInfo> GetReadChunkInfo(ulong chunkIndex)
    {
        // H5Dfarray.c (H5D__farray_idx_get_addr)

        /* Check for filters on chunks */
        if (Dataset.FilterPipeline is not null)
        {
            var element = await GetElement(chunkIndex, async driver =>
            {
                return new FilteredDataBlockElement(
                    Address: await ReadContext.Superblock.ReadOffset(driver).ConfigureAwait(false),
                    ChunkSize: (uint)await ReadUtils.ReadUlong(driver, ChunkSizeLength).ConfigureAwait(false),
                    FilterMask: await driver.ReadUInt32().ConfigureAwait(false)
                );
            }).ConfigureAwait(false);

            return element is not null
                ? new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask)
                : ChunkInfo.None;
        }

        else
        {
            var element = await GetElement(chunkIndex, async driver =>
            {
                return new DataBlockElement(
                    Address: await ReadContext.Superblock.ReadOffset(driver).ConfigureAwait(false)
                );
            }).ConfigureAwait(false);

            return element is not null
                ? new ChunkInfo(element.Address, ChunkByteSize, 0)
                : ChunkInfo.None;
        }
    }

    protected override ChunkInfo GetActualWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask)
    {
        /* Check for filters on chunks */
        ChunkInfo chunkInfo;

        if (Dataset.FilterPipeline is not null)
        {
            chunkInfo = new ChunkInfo(
                Address: (ulong)WriteContext.FreeSpaceManager.Allocate(chunkSize, AllocationKind.RawData),
                Size: chunkSize,
                FilterMask: filterMask
            );
        }

        else
        {
            throw new Exception("This should never happen.");
        }

        return chunkInfo;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (WriteContext is not null)
        {
            if (Dataset.FilterPipeline is not null)
            {
                var layout = (DataLayoutMessage4)Dataset.Layout;
                var properties = (ChunkedStoragePropertyDescription4)layout.Properties;
                var indexingInformation = (FixedArrayIndexingInformation)properties.IndexingInformation;
                var pageBits = indexingInformation.PageBits;
                var elements = new FilteredDataBlockElement[WriteChunkInfos.Length];

                for (int i = 0; i < WriteChunkInfos.Length; i++)
                {
                    var chunkInfo = WriteChunkInfos[i];

                    elements[i] = new FilteredDataBlockElement(
                        Address: chunkInfo.Address,
                        ChunkSize: (uint)chunkInfo.Size,
                        FilterMask: chunkInfo.FilterMask
                    );
                }

                /* H5FA__cache_hdr_serialize (H5FAcache.c) */
                var entriesCount = (ulong)WriteChunkInfos.Length;
                var entrySize = FilteredDataBlockElement.GetEncodeSize(ChunkSizeLength);
                var (_, pageCount, pageBitmapSize) = GetInfo(pageBits, entriesCount);

                var dataBlock = new FixedArrayDataBlock<FilteredDataBlockElement>(
                    ClientID: ClientID.FilteredDatasetChunks,
                    HeaderAddress: Chunked4.Address,
                    PageBitmap: Array.Empty<byte>(),
                    Elements: elements,
                    ElementsPerPage: default,
                    PageCount: pageCount,
                    LastPageElementCount: default
                )
                {
                    Version = 0
                };

                var dataBlockEncodeSize = dataBlock.GetEncodeSize(pageCount, pageBitmapSize, entrySize);
                var dataBlockAddress = WriteContext.FreeSpaceManager.Allocate((long)dataBlockEncodeSize, AllocationKind.Metadata);

                var header = new FixedArrayHeader(
                    Superblock: default!,
                    ClientID: ClientID.FilteredDatasetChunks,
                    EntrySize: entrySize,
                    PageBits: pageBits,
                    EntriesCount: entriesCount,
                    DataBlockAddress: (ulong)dataBlockAddress)
                {
                    Version = 0
                };

                // header
                WriteContext.Driver.SeekRelativeToBaseAddress((long)Chunked4.Address);

                // SYNC SURFACE: Encode is async and this is the synchronous write path, so it must
                // block. Unawaited it would race the data-block write below.
                header.Encode(WriteContext.Driver).GetAwaiter().GetResult();

                // data block
                WriteContext.Driver.SeekRelativeToBaseAddress(dataBlockAddress);

                dataBlock.Encode(
                    driver: WriteContext.Driver,
                    encode: (driver, element) =>
                {
                    // Address
                    driver.Write(element.Address);

                    // Chunk Size
                    WriteUtils.WriteUlongArbitrary(driver, element.ChunkSize, ChunkSizeLength);

                    // Filter Mask
                    driver.Write(element.FilterMask);
                });
            }

            else
            {
                throw new Exception("This should never happen.");
            }
        }
    }

    private static (ulong, ulong, ulong) GetInfo(byte pageBits, ulong entriesCount)
    {
        // H5FAdblock.c (H5FA__dblock_alloc)
        var elementsPerPage = 1UL << pageBits;
        var pageCount = 0UL;
        var pageBitmapSize = 0UL;

        if (entriesCount > elementsPerPage)
        {
            /* Compute number of pages */
            pageCount = (entriesCount + elementsPerPage - 1) / elementsPerPage;

            /* Compute size of 'page init' flag array, in bytes */
            pageBitmapSize = (pageCount + 7) / 8;
        }

        return (elementsPerPage, pageCount, pageBitmapSize);
    }

    private async ValueTask<T?> GetElement<T>(ulong index, Func<H5DriverBase, ValueTask<T>> decode) where T : DataBlockElement
    {
        // Cached per file and per address, not per H5D_Base: NativeDataset builds a fresh H5D_Base for
        // every Read (NativeDataset.cs), so anything held in a field here dies with the call and every
        // read of a chunked dataset re-decodes the chunk index from scratch.
        _header ??= await NativeCache
            .GetStructure(ReadContext, Chunked4.Address, FixedArrayHeader.Decode)
            .ConfigureAwait(false);

        // H5FA.c (H5FA_get)

        /* Check if the fixed array data block has been already allocated on disk */
        if (ReadContext.Superblock.IsUndefinedAddress(_header.DataBlockAddress))
        {
            /* Call the class's 'fill' callback */
            return null;
        }

        else
        {
            var cached = await NativeCache
                .GetStructure(
                    ReadContext,
                    _header.DataBlockAddress,
                    (Header: _header, Decode: decode),
                    static async (context, state) =>
                    {
                        // H5FA.c (H5FA_get)
                        var (elementsPerPage, pageCount, pageBitmapSize) = GetInfo(
                            state.Header.PageBits,
                            state.Header.EntriesCount
                        );

                        var dataBlock = await FixedArrayDataBlock<T>.Decode(
                            context,
                            elementsPerPage,
                            pageCount,
                            pageBitmapSize,
                            state.Header.EntriesCount,
                            state.Decode).ConfigureAwait(false);

                        // Wherever the decode left the cursor - see CachedIndex.
                        return new CachedIndex<T>(
                            dataBlock,
                            context.Driver.Position,
                            new BoundedAddressCache<DataBlockPage<T>>());
                    })
                .ConfigureAwait(false);

            return await LookupElement(
                _header,
                cached,
                index,
                decode
            ).ConfigureAwait(false);
        }
    }

    private async ValueTask<T?> LookupElement<T>(
        FixedArrayHeader header,
        CachedIndex<T> cached,
        ulong index,
        Func<H5DriverBase, ValueTask<T>> decode
    )
        where T : DataBlockElement
    {
        var dataBlock = cached.DataBlock;

        /* Check for paged data block */
        if (dataBlock.PageCount > 0)
        {
            /* Compute the page index */
            var pageIndex = index / dataBlock.ElementsPerPage;
            var pageBitmapEntry = dataBlock.PageBitmap[pageIndex / 8];
            var bitMaskIndex = (int)pageIndex % 8;

            /* Check if the page is defined yet */
            if ((pageBitmapEntry & MathUtils.SequentialBitMask[bitMaskIndex]) > 0)
            {
                /* Compute the element index */
                var elementIndex = index % dataBlock.ElementsPerPage;

                /* Compute the address of the data block */
                var pageSize = dataBlock.ElementsPerPage * header.EntrySize + 4;
                var pageAddress = cached.FirstPageAddress + (long)(pageIndex * pageSize);

                /* Check for using last page, to set the number of elements on the page */
                ulong elementCount;

                if (pageIndex + 1 == dataBlock.PageCount)
                    elementCount = dataBlock.LastPageElementCount;

                else
                    elementCount = dataBlock.ElementsPerPage;

                /* Decode the data block page */
                if (!cached.Pages.TryGetValue((ulong)pageAddress, out var page))
                {
                    ReadContext.Driver.SeekRelativeToBaseAddress(pageAddress);

                    var decoded = await DataBlockPage<T>.Decode(
                        ReadContext.Driver,
                        elementCount,
                        decode
                    ).ConfigureAwait(false);

                    page = cached.Pages.GetOrAdd((ulong)pageAddress, decoded);
                }

                var elements = page.Elements;

                /* Retrieve element from data block */
                return elements[elementIndex];
            }
            else
            {
                /* Call the class's 'fill' callback */
                return null;
            }
        }
        else
        {
            /* Retrieve element from data block */
            return dataBlock.Elements[index];
        }
    }
}
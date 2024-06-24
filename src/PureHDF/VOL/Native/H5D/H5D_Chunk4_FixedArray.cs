using System.Collections.Concurrent;

namespace PureHDF.VOL.Native;

internal class H5D_Chunk4_FixedArray : H5D_Chunk4
{
    private FixedArrayHeader? _header;

    private object? _dataBlock;

    private long _firstPageAddress;

    private ConcurrentDictionary<long, object> _addressToObjectMap { get; } = new();

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

    protected override ChunkInfo GetReadChunkInfo(ulong chunkIndex)
    {
        // H5Dfarray.c (H5D__farray_idx_get_addr)

        /* Check for filters on chunks */
        if (Dataset.FilterPipeline is not null)
        {
            var element = GetElement(chunkIndex, driver =>
            {
                return new FilteredDataBlockElement(
                    Address: ReadContext.Superblock.ReadOffset(driver),
                    ChunkSize: (uint)ReadUtils.ReadUlong(driver, ChunkSizeLength),
                    FilterMask: driver.ReadUInt32()
                );
            });

            return element is not null
                ? new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask)
                : ChunkInfo.None;
        }

        else
        {
            var element = GetElement(chunkIndex, driver =>
            {
                return new DataBlockElement(
                    Address: ReadContext.Superblock.ReadOffset(driver)
                );
            });

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
                Address: (ulong)WriteContext.FreeSpaceManager.Allocate(chunkSize),
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
                var dataBlockAddress = WriteContext.FreeSpaceManager.Allocate((long)dataBlockEncodeSize);

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
                WriteContext.Driver.Seek((long)Chunked4.Address, SeekOrigin.Begin);
                header.Encode(WriteContext.Driver);

                // data block
                WriteContext.Driver.Seek(dataBlockAddress, SeekOrigin.Begin);

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

    private T? GetElement<T>(ulong index, Func<H5DriverBase, T> decode) where T : DataBlockElement
    {
        if (_header is null)
        {
            ReadContext.Driver.Seek((long)Chunked4.Address, SeekOrigin.Begin);
            _header = FixedArrayHeader.Decode(ReadContext);
        }

        // H5FA.c (H5FA_get)

        /* Check if the fixed array data block has been already allocated on disk */
        if (ReadContext.Superblock.IsUndefinedAddress(_header.DataBlockAddress))
        {
            /* Call the class's 'fill' callback */
            return null;
        }

        else
        {
            if (_dataBlock is null)
            {
                // H5FA.c (H5FA_get)

                /* Get the data block */
                ReadContext.Driver.Seek((long)_header.DataBlockAddress, SeekOrigin.Begin);

                var (elementsPerPage, pageCount, pageBitmapSize) = GetInfo(
                    _header.PageBits, 
                    _header.EntriesCount
                );

                _dataBlock = FixedArrayDataBlock<T>.Decode(
                    ReadContext,
                    elementsPerPage,
                    pageCount,
                    pageBitmapSize,
                    _header.EntriesCount,
                    decode);

                _firstPageAddress = ReadContext.Driver.Position;
            }

            return LookupElement(
                _header, 
                (FixedArrayDataBlock<T>)_dataBlock, 
                index, 
                decode
            );
        }
    }

    private T? LookupElement<T>(
        FixedArrayHeader header, 
        FixedArrayDataBlock<T> dataBlock, 
        ulong index, 
        Func<H5DriverBase, T> decode
    ) 
        where T : DataBlockElement
    {
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
                var pageAddress = _firstPageAddress + (long)(pageIndex * pageSize);

                /* Check for using last page, to set the number of elements on the page */
                ulong elementCount;

                if (pageIndex + 1 == dataBlock.PageCount)
                    elementCount = dataBlock.LastPageElementCount;

                else
                    elementCount = dataBlock.ElementsPerPage;

                /* Decode the data block page */
                var page = (DataBlockPage<T>)_addressToObjectMap
                    .GetOrAdd(pageAddress, address =>
                {
                    ReadContext.Driver.Seek(address, SeekOrigin.Begin);

                    return DataBlockPage<T>.Decode(
                        ReadContext.Driver,
                        elementCount,
                        decode
                    );
                });

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
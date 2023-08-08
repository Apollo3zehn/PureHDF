namespace PureHDF;

internal class H5D_Chunk4_FixedArray : H5D_Chunk4
{
    private FixedArrayHeader? _header;

    private List<ChunkInfo>? _writeChunkInfos;

    public H5D_Chunk4_FixedArray(NativeReadContext readContext, NativeWriteContext writeContext, DatasetInfo dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
        base(readContext, writeContext, dataset, layout, datasetAccess)
    {
        //
    }

    protected override ChunkInfo GetReadChunkInfo(ulong[] chunkIndices)
    {
        // H5Dfarray.c (H5D__farray_idx_get_addr)

        /* Calculate the index of this chunk */
        var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(DownMaxChunkCounts);

        /* Check for filters on chunks */
        if (Dataset.FilterPipeline is not null)
        {
            var element = GetElement(chunkIndex, driver =>
            {
                return new FilteredDataBlockElement(
                    Address: ReadContext.Superblock.ReadOffset(driver),
                    ChunkSize: (uint)ReadUtils.ReadUlong(driver, ChunkByteSize),
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

    protected override ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize)
    {
        /* Check for filters on chunks */
        ChunkInfo chunkInfo;

        if (Dataset.FilterPipeline is not null)
        {
            chunkInfo = new ChunkInfo(
                Address: (ulong)WriteContext.FreeSpaceManager.Allocate(chunkSize),
                Size: 0,
                FilterMask: 0
            );
        }

        else
        {
            throw new Exception("This should never happen.");
        }

        _writeChunkInfos ??= new();

        if (_writeChunkInfos.Contains(chunkInfo))
            throw new Exception("Chunks can only be written once. Consider increasing the size of the chunk cache to avoid chunks written to disk too early.");

        else
            _writeChunkInfos.Add(chunkInfo);

        return chunkInfo;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (WriteContext is not null && _writeChunkInfos is not null)
        {
            if (Dataset.FilterPipeline is not null)
            {
                var elements = new FilteredDataBlockElement[_writeChunkInfos.Count];

                var dataBlock = new FixedArrayDataBlock<FilteredDataBlockElement>(
                    ClientID: ClientID.FilteredDatasetChunks,
                    HeaderAddress: Layout.Address, 
                    PageBitmap: Array.Empty<byte>(),
                    Elements: elements,
                    ElementsPerPage: default,
                    PageCount: default,
                    LastPageElementCount: default
                )
                {
                    Version = 0
                };

                var dataBlockEncodeSize = dataBlock.GetEncodeSize();
                var dataBlockAddress = WriteContext.FreeSpaceManager.Allocate((long)dataBlockEncodeSize);

                var header = new FixedArrayHeader(
                    Superblock: default!,
                    ClientID: ClientID.FilteredDatasetChunks,
                    EntrySize: FilteredDataBlockElement.ENCODE_SIZE,
                    PageBits: 0,
                    EntriesCount: (ulong)_writeChunkInfos.Count,
                    DataBlockAddress: (ulong)dataBlockAddress)
                {
                    Version = 0
                };

                WriteContext.Driver.Seek((long)Layout.Address, SeekOrigin.Begin);
                header.Encode(WriteContext.Driver);

                WriteContext.Driver.Seek(dataBlockAddress, SeekOrigin.Begin);
                dataBlock.Encode(WriteContext.Driver);
            }

            else
            {
                throw new Exception("This should never happen.");
            }
        }
    }

    private T? GetElement<T>(ulong index, Func<H5DriverBase, T> decode) where T : DataBlockElement
    {
        if (_header is null)
        {
            ReadContext.Driver.Seek((long)Dataset.Layout.Address, SeekOrigin.Begin);
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
            return LookupElement(_header, index, decode);
        }
    }

    private T? LookupElement<T>(FixedArrayHeader header, ulong index, Func<H5DriverBase, T> decode) where T : DataBlockElement
    {
        // H5FA.c (H5FA_get)

        /* Get the data block */
        ReadContext.Driver.Seek((long)header.DataBlockAddress, SeekOrigin.Begin);

        var dataBlock = FixedArrayDataBlock<T>.Decode(
            ReadContext,
            header,
            decode);

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
                var pageAddress = ReadContext.Driver.Position + (long)(pageIndex * pageSize);

                /* Check for using last page, to set the number of elements on the page */
                ulong elementCount;

                if (pageIndex + 1 == dataBlock.PageCount)
                    elementCount = dataBlock.LastPageElementCount;

                else
                    elementCount = dataBlock.ElementsPerPage;

                /* Protect the data block page */
                ReadContext.Driver.Seek(pageAddress, SeekOrigin.Begin);

                var page = DataBlockPage<T>.Decode(
                    ReadContext.Driver,
                    elementCount,
                    decode);

                /* Retrieve element from data block */
                return page.Elements[elementIndex];
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
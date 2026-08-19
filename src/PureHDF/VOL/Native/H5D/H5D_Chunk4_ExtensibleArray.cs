namespace PureHDF.VOL.Native;

internal class H5D_Chunk4_ExtensibleArray : H5D_Chunk4
{
    /// <summary>
    /// Everything a repeated read needs to reuse below the header: the index block, and the secondary
    /// blocks, data blocks and data block pages reached through it.
    /// </summary>
    /// <remarks>
    /// <c>Blocks</c> is keyed by file address and holds three different kinds of object, which is safe
    /// because the three live at distinct addresses, so one key space cannot confuse them.
    /// <para>
    /// Bounded, because the number of secondary blocks, data blocks and pages grows with the chunk
    /// count - the same reason the b-tree node caches are bounded. Counted rather than byte-costed:
    /// these are index structures of broadly similar size, not payload.
    /// </para>
    /// </remarks>
    private sealed record class CachedIndex<T>(
        ExtensibleArrayIndexBlock<T> IndexBlock,
        BoundedAddressCache<object> Blocks
    ) where T : DataBlockElement;

    #region Fields

    private int _unlimitedDim;

    // these fields will all be initialized in Initialize()
    private ulong[] _swizzledChunkDims = default!;
    private ulong[] _swizzledDownMaxChunkCounts = default!;

    private ExtensibleArrayHeader? _header;

    #endregion

    #region Constructors

    public H5D_Chunk4_ExtensibleArray(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        DataLayoutMessage4 layout,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation) :
        base(readContext, writeContext, dataset, layout, datasetAccess, datasetCreation)
    {
        //
    }

    #endregion

    #region Methods

    public override void Initialize()
    {
        base.Initialize();

        _unlimitedDim = Dataset.Space.MaxDimensions
            .ToList()
            .FindLastIndex(value => value == H5Constants.Unlimited);

        // H5Dearray.c (H5D__earray_idx_resize)

        /* "Swizzle" constant dimensions for this dataset */
        if (_unlimitedDim > 0)
        {
            /* Get the swizzled chunk dimensions */
            _swizzledChunkDims = ChunkDims.ToArray();
            MathUtils.SwizzleCoords(_swizzledChunkDims, _unlimitedDim);

            /* Get the swizzled number of chunks in each dimension */
            var swizzledScaledDims = ScaledDims.ToArray();
            MathUtils.SwizzleCoords(swizzledScaledDims, _unlimitedDim);

            /* Get the swizzled "down" sizes for each dimension */
            // _swizzledDownChunkCounts = swizzledScaledDims.AccumulateReverse();

            /* Get the swizzled max number of chunks in each dimension */
            var swizzledScaledMaxDims = ScaledMaxDims.ToArray();
            MathUtils.SwizzleCoords(swizzledScaledMaxDims, _unlimitedDim);

            /* Get the swizzled max "down" sizes for each dimension */
            _swizzledDownMaxChunkCounts = swizzledScaledMaxDims.AccumulateReverse();
        }
    }

    protected override async ValueTask<ChunkInfo> GetReadChunkInfo(ulong chunkIndex)
    {
        // H5Dearray.c (H5D__earray_idx_get_addr)

        /* Check for unlimited dim. not being the slowest-changing dim. */
        if (_unlimitedDim > 0)
        {
            var chunkIndices = MathUtils.ToCoordinates(chunkIndex, ScaledDims);
            var swizzledCoords = new ulong[ChunkRank];

            /* Compute coordinate offset from scaled offset */
            for (int i = 0; i < ChunkRank; i++)
            {
                swizzledCoords[i] = chunkIndices[i] * ChunkDims[i];
            }

            MathUtils.SwizzleCoords(swizzledCoords, _unlimitedDim);

            /* Calculate the index of this chunk */
            var swizzledScaledDims = swizzledCoords
                .Select((swizzledCoord, i) => MathUtils.CeilDiv(swizzledCoord, _swizzledChunkDims[i]))
                .ToArray();

            chunkIndex = swizzledScaledDims.ToLinearIndexPrecomputed(_swizzledDownMaxChunkCounts);
        }

        /* Check for filters on chunks */
        if (Dataset.FilterPipeline is not null)
        {
            var chunkSizeLength = MathUtils.ComputeChunkSizeLength(ChunkByteSize);

            var element = await GetElement(chunkIndex, async driver =>
            {
                return new FilteredDataBlockElement(
                    Address: await ReadContext.Superblock.ReadOffset(driver).ConfigureAwait(false),
                    ChunkSize: (uint)await ReadUtils.ReadUlong(driver, chunkSizeLength).ConfigureAwait(false),
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
        throw new NotImplementedException();
    }

    private async ValueTask<T?> GetElement<T>(ulong index, Func<H5DriverBase, ValueTask<T>> decode) where T : DataBlockElement
    {
        // Cached per file and per address, not per H5D_Base: NativeDataset builds a fresh H5D_Base for
        // every Read (NativeDataset.cs), so anything held in a field here dies with the call and every
        // read of a chunked dataset re-decodes the chunk index from scratch.
        _header ??= await NativeCache
            .GetStructure(ReadContext, Chunked4.Address, ExtensibleArrayHeader.Decode)
            .ConfigureAwait(false);

        // H5EA.c (H5EA_get)

        /* Check for element beyond max. element in array */
        if (index >= _header.MaximumIndexSet)
        {
            /* Call the class's 'fill' callback */
            return null;
        }
        else
        {
            /* Check if we should create the index block */
            if (ReadContext.Superblock.IsUndefinedAddress(_header.IndexBlockAddress))
                return null;

            /* Get the index block */
            var cached = await NativeCache
                .GetStructure(
                    ReadContext,
                    _header.IndexBlockAddress,
                    (Header: _header, Decode: decode),
                    static async (context, state) =>
                    {
                        var indexBlock = await ExtensibleArrayIndexBlock<T>.Decode(
                            context.Driver,
                            context.Superblock,
                            state.Header,
                            state.Decode).ConfigureAwait(false);

                        return new CachedIndex<T>(indexBlock, new BoundedAddressCache<object>());
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
        ExtensibleArrayHeader header,
        CachedIndex<T> cached,
        ulong index,
        Func<H5DriverBase, ValueTask<T>> decode) where T : DataBlockElement
    {
        // H5EA.c (H5EA__lookup_elmt)
        var indexBlock = cached.IndexBlock;
        var blocks = cached.Blocks;

        /* Check if element is in index block */
        if (index < header.IndexBlockElementsCount)
        {
            return indexBlock.Elements[index];
        }

        else
        {
            /* Get super block index where element is located */
            var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(index);

            /* Adjust index to offset in super block */
            var elementIndex = index - (header.IndexBlockElementsCount + header.SecondaryBlockInfos[secondaryBlockIndex].ElementStartIndex);

            /* Check for data block containing element address in the index block */
            if (secondaryBlockIndex < indexBlock.SecondaryBlockDataBlockAddressCount)
            {
                /* Compute the data block index in index block */
                var dataBlockIndex =
                    header.SecondaryBlockInfos[secondaryBlockIndex].DataBlockStartIndex +
                    elementIndex / header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                /* Check if the data block has been allocated on disk yet */
                if (ReadContext.Superblock.IsUndefinedAddress(indexBlock.DataBlockAddresses[dataBlockIndex]))
                    return null;

                /* Get data block */
                var dataBlockAddress = indexBlock.DataBlockAddresses[dataBlockIndex];

                if (!blocks.TryGetValue(dataBlockAddress, out var dataBlockObj))
                {
                    var elementsCount = header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                    ReadContext.Driver.SeekRelativeToBaseAddress((long)dataBlockAddress);

                    var decoded = await ExtensibleArrayDataBlock<T>.Decode(
                        ReadContext,
                        header,
                        elementsCount,
                        decode).ConfigureAwait(false);

                    dataBlockObj = blocks.GetOrAdd(dataBlockAddress, decoded);
                }

                var dataBlock = (ExtensibleArrayDataBlock<T>)dataBlockObj;

                /* Adjust index to offset in data block */
                elementIndex %= header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                /* Set 'thing' info to refer to the data block */
                return dataBlock.Elements[elementIndex];
            }

            else
            {
                /* Calculate offset of super block in index block's array */
                var secondaryBlockOffset = secondaryBlockIndex - indexBlock.SecondaryBlockDataBlockAddressCount;

                /* Check if the super block has been allocated on disk yet */
                if (ReadContext.Superblock.IsUndefinedAddress(indexBlock.SecondaryBlockAddresses[secondaryBlockOffset]))
                    return null;

                /* Get super block */
                var secondaryBlockAddress = indexBlock.SecondaryBlockAddresses[secondaryBlockOffset];

                if (!blocks.TryGetValue(secondaryBlockAddress, out var secondaryBlockObj))
                {
                    ReadContext.Driver.SeekRelativeToBaseAddress((long)secondaryBlockAddress);

                    var decoded = await ExtensibleArraySecondaryBlock.Decode(
                        ReadContext,
                        header,
                        secondaryBlockIndex).ConfigureAwait(false);

                    secondaryBlockObj = blocks.GetOrAdd(secondaryBlockAddress, decoded);
                }

                var secondaryBlock = (ExtensibleArraySecondaryBlock)secondaryBlockObj;

                /* Compute the data block index in super block */
                var dataBlockIndex = elementIndex / secondaryBlock.ElementCount;

                /* Check if the data block has been allocated on disk yet */
                if (ReadContext.Superblock.IsUndefinedAddress(secondaryBlock.DataBlockAddresses[dataBlockIndex]))
                    return null;

                /* Adjust index to offset in data block */
                elementIndex %= secondaryBlock.ElementCount;

                /* Check if the data block is paged */
                if (secondaryBlock.DataBlockPageCount > 0)
                {
                    /* Compute page index */
                    var pageIndex = elementIndex / header.DataBlockPageElementsCount;

                    /* Compute 'page init' index */
                    var pageInitIndex = dataBlockIndex * secondaryBlock.DataBlockPageCount + pageIndex;

                    /* Adjust index to offset in data block page */
                    elementIndex %= header.DataBlockPageElementsCount;

                    /* Compute data block page address */
                    var dataBlockPrefixSize =
                        // H5EA_METADATA_PREFIX_SIZE
                        4UL + 1UL + 1UL + 4UL +
                        // H5EA_DBLOCK_PREFIX_SIZE
                        ReadContext.Superblock.OffsetsSize + header.ArrayOffsetsSize +
                        // H5EA_DBLOCK_SIZE
                        secondaryBlock.ElementCount * header.ElementSize +      /* Elements in data block */
                        secondaryBlock.DataBlockPageCount * 4;                  /* Checksum for each page */

                    var dataBlockPageAddress = secondaryBlock.DataBlockAddresses[dataBlockIndex] + dataBlockPrefixSize +
                                (pageIndex * secondaryBlock.DataBlockPageSize);

                    /* Check if page has been initialized yet */
                    var pageBitmapEntry = secondaryBlock.PageBitmap[pageIndex / 8];
                    var bitMaskIndex = (int)pageIndex % 8;

                    if ((pageBitmapEntry & MathUtils.SequentialBitMask[bitMaskIndex]) == 0)
                        return null;

                    /* Get data block page */
                    if (!blocks.TryGetValue(dataBlockPageAddress, out var dataBlockPageObj))
                    {
                        ReadContext.Driver.SeekRelativeToBaseAddress((long)dataBlockPageAddress);

                        var decoded = await DataBlockPage<T>.Decode(
                            ReadContext.Driver,
                            header.DataBlockPageElementsCount,
                            decode).ConfigureAwait(false);

                        dataBlockPageObj = blocks.GetOrAdd(dataBlockPageAddress, decoded);
                    }

                    var dataBlockPage = (DataBlockPage<T>)dataBlockPageObj;

                    /* Set 'thing' info to refer to the data block page */
                    return dataBlockPage.Elements[elementIndex];
                }

                else
                {
                    /* Get data block */
                    var dataBlockAddress = secondaryBlock.DataBlockAddresses[dataBlockIndex];

                    if (!blocks.TryGetValue(dataBlockAddress, out var dataBlockObj))
                    {
                        ReadContext.Driver.SeekRelativeToBaseAddress((long)dataBlockAddress);

                        var decoded = await ExtensibleArrayDataBlock<T>.Decode(
                            ReadContext,
                            header,
                            secondaryBlock.ElementCount,
                            decode).ConfigureAwait(false);

                        dataBlockObj = blocks.GetOrAdd(dataBlockAddress, decoded);
                    }

                    var dataBlock = (ExtensibleArrayDataBlock<T>)dataBlockObj;

                    /* Set 'thing' info to refer to the data block */
                    return dataBlock.Elements[elementIndex];
                }
            }
        }
    }

    #endregion
}
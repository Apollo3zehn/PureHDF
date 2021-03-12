using System.IO;
using System.Linq;

namespace HDF5.NET
{
    internal class H5D_Chunk4_ExtensibleArray : H5D_Chunk4
    {
        #region Fields

        private int _unlimitedDim;

        private ulong[] _swizzledChunkDims;
        private ulong[] _swizzledDownChunkCounts;
        private ulong[] _swizzledDownMaxChunkCounts;

        private ExtensibleArrayHeader? _header;
        private ExtensibleArrayIndexBlock? _indexBlock;

        #endregion

        #region Constructors

        public H5D_Chunk4_ExtensibleArray(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
            base(dataset, layout, datasetAccess)
        {
            //
        }

        #endregion

        #region Methods

        public override void Initialize()
        {
            base.Initialize();

            _unlimitedDim = this.Dataset.Dataspace.DimensionMaxSizes
               .ToList()
               .FindLastIndex(value => value == H5Constants.Unlimited);

            // H5Dearray.c (H5D__earray_idx_resize)

            /* "Swizzle" constant dimensions for this dataset */
            if (_unlimitedDim > 0)
            {
                /* Get the swizzled chunk dimensions */
                _swizzledChunkDims = this.ChunkDims.ToArray();
                H5Utils.SwizzleCoords(_swizzledChunkDims, _unlimitedDim);

                /* Get the swizzled number of chunks in each dimension */
                var swizzledScaledDims = this.ScaledDims.ToArray();
                H5Utils.SwizzleCoords(swizzledScaledDims, _unlimitedDim);

                /* Get the swizzled "down" sizes for each dimension */
                _swizzledDownChunkCounts = swizzledScaledDims.AccumulateReverse();

                /* Get the swizzled max number of chunks in each dimension */
                var swizzledScaledMaxDims = this.ScaledMaxDims.ToArray();
                H5Utils.SwizzleCoords(swizzledScaledMaxDims, _unlimitedDim);

                /* Get the swizzled max "down" sizes for each dimension */
                _swizzledDownMaxChunkCounts = swizzledScaledMaxDims.AccumulateReverse();
            }
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            // H5Dearray.c (H5D__earray_idx_get_addr)

            /* Check for unlimited dim. not being the slowest-changing dim. */
            ulong chunkIndex;

            if (_unlimitedDim > 0)
            {
                var swizzledCoords = new ulong[this.ChunkRank];

                /* Compute coordinate offset from scaled offset */
                for (int i = 0; i < this.ChunkRank; i++)
                {
                    swizzledCoords[i] = chunkIndices[i] * this.ChunkDims[i];
                }

                H5Utils.SwizzleCoords(swizzledCoords, _unlimitedDim);

                /* Calculate the index of this chunk */
                var swizzledScaledDims = swizzledCoords
                    .Select((swizzledCoord, i) => H5Utils.CeilDiv(swizzledCoord, _swizzledChunkDims[i]))
                    .ToArray();

                chunkIndex = swizzledScaledDims.ToLinearIndex(_swizzledDownMaxChunkCounts);
            }
            else
            {
                /* Calculate the index of this chunk */
                chunkIndex = chunkIndices.ToLinearIndex(this.DownMaxChunkCounts);
            }

            /* Check for filters on chunks */
            if (this.Dataset.FilterPipeline is not null)
            {
                return this.GetElement(chunkIndex);
            }
            else
            {
                return this.GetElement(chunkIndex) with
                {
                    Size = this.ChunkByteSize
                };
            }
        }

        private ChunkInfo GetElement(ulong index)
        {
            if (_header is null)
                _header = new ExtensibleArrayHeader(this.Dataset.Context.Reader, this.Dataset.Context.Superblock);

            // H5EA.c (H5EA_get)

            /* Check for element beyond max. element in array */
            if (index >= _header.MaximumIndexSet)
            {
                /* Call the class's 'fill' callback */
                return ChunkInfo.None;
            }
            else
            {
                /* Look up the array metadata containing the element we want to set */
                return this.LookupElement(index);
            }
        }

        private ChunkInfo LookupElement(ulong index)
        {
            // H5EA.c (H5EA__lookup_elmt)
            var chunkSizeLength = H5Utils.ComputeChunkSizeLength(this.ChunkByteSize);

            /* Check if we should create the index block */
            if (this.Dataset.Context.Superblock.IsUndefinedAddress(_header.IndexBlockAddress))
                return ChunkInfo.None;

            /* Protect index block */
            if (_indexBlock is null)
            {
                this.Dataset.Context.Reader.Seek((long)_header.IndexBlockAddress, SeekOrigin.Begin);
                _indexBlock = new ExtensibleArrayIndexBlock(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, _header, chunkSizeLength);
            }

            /* Check if element is in index block */
            if (index < _header.IndexBlockElementsCount)
            {
                var element = _indexBlock.Elements[index];
                return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
            }
            else
            {
                /* Get super block index where element is located */
                var secondaryBlockIndex = _header.ComputeSecondaryBlockIndex(index);

                /* Adjust index to offset in super block */
                var elementIndex = index - (_header.IndexBlockElementsCount + _header.SecondaryBlockInfos[secondaryBlockIndex].ElementStartIndex);

                /* Check for data block containing element address in the index block */
                if (secondaryBlockIndex < _indexBlock.SecondaryBlockDataBlockAddressCount)
                {
                    /* Compute the data block index in index block */
                    var dataBlockIndex =
                        _header.SecondaryBlockInfos[secondaryBlockIndex].DataBlockStartIndex +
                        elementIndex / _header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                    /* Check if the data block has been allocated on disk yet */
                    if (this.Dataset.Context.Superblock.IsUndefinedAddress(_indexBlock.DataBlockAddresses[dataBlockIndex]))
                        return ChunkInfo.None;

                    /* Protect data block */
                    this.Dataset.Context.Reader.Seek((long)_indexBlock.DataBlockAddresses[dataBlockIndex], SeekOrigin.Begin);
                    var elementsCount = _header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                    var dataBlock = new ExtensibleArrayDataBlock(
                        this.Dataset.Context.Reader,
                        this.Dataset.Context.Superblock,
                        _header,
                        chunkSizeLength,
                        elementsCount);

                    /* Adjust index to offset in data block */
                    elementIndex %= _header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;

                    /* Set 'thing' info to refer to the data block */
                    var element = dataBlock.Elements[elementIndex];
                    return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
                }
                else
                {
                    /* Calculate offset of super block in index block's array */
                    var secondaryBlockOffset = secondaryBlockIndex - _indexBlock.SecondaryBlockDataBlockAddressCount;

                    /* Check if the super block has been allocated on disk yet */
                    if (this.Dataset.Context.Superblock.IsUndefinedAddress(_indexBlock.SecondaryBlockAddresses[secondaryBlockOffset]))
                        return ChunkInfo.None;

                    /* Protect super block */
                    this.Dataset.Context.Reader.Seek((long)_indexBlock.SecondaryBlockAddresses[secondaryBlockOffset], SeekOrigin.Begin);
                    var secondaryBlock = new ExtensibleArraySecondaryBlock(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, _header, secondaryBlockIndex);

                    /* Compute the data block index in super block */
                    var dataBlockIndex = elementIndex / secondaryBlock.ElementsCount;

                    /* Check if the data block has been allocated on disk yet */
                    if (this.Dataset.Context.Superblock.IsUndefinedAddress(secondaryBlock.DataBlockAddresses[dataBlockIndex]))
                        return ChunkInfo.None;

                    /* Adjust index to offset in data block */
                    elementIndex %= secondaryBlock.ElementsCount;

                    /* Check if the data block is paged */
                    if (secondaryBlock.DataBlockPageCount > 0)
                    {
                        /* Compute page index */
                        var pageIndex = elementIndex / _header.DataBlockPageElementsCount;

                        /* Compute 'page init' index */
                        var pageInitIndex = dataBlockIndex * secondaryBlock.DataBlockPageCount + pageIndex;

                        /* Adjust index to offset in data block page */
                        elementIndex %= _header.DataBlockPageElementsCount;

                        /* Compute data block page address */
                        var dataBlockPrefixSize =
                            // H5EA_METADATA_PREFIX_SIZE
                            4UL + 1UL + 1UL + 4UL +
                            // H5EA_DBLOCK_PREFIX_SIZE
                            this.Dataset.Context.Superblock.OffsetsSize + _header.ArrayOffsetsSize +
                            // H5EA_DBLOCK_SIZE
                            secondaryBlock.ElementsCount * _header.ElementSize +    /* Elements in data block */
                            secondaryBlock.DataBlockPageCount * 4;                  /* Checksum for each page */

                        var dataBlockPageAddress = secondaryBlock.DataBlockAddresses[dataBlockIndex] + dataBlockPrefixSize +
                                 (pageIndex * secondaryBlock.DataBlockPageSize);

                        /* Check if page has been initialized yet */
                        var pageBitmapEntry = secondaryBlock.PageBitmap[pageIndex / 8];
                        var bitMaskIndex = (int)pageIndex % 8;

                        if ((pageBitmapEntry & H5Utils.SequentialBitMask[bitMaskIndex]) == 0)
                            return ChunkInfo.None;

                        /* Protect data block page */
                        this.Dataset.Context.Reader.Seek((long)dataBlockPageAddress, SeekOrigin.Begin);
                        var dataBlockPage = new DataBlockPage(this.Dataset.Context.Reader,
                                                     this.Dataset.Context.Superblock,
                                                     _header.DataBlockPageElementsCount,
                                                     dataBlock.ClientID,
                                                     chunkSizeLength);

                        /* Set 'thing' info to refer to the data block page */
                        var element = dataBlockPage.Elements[elementIndex];
                        return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
                    }
                    else
                    {
                        /* Protect data block */
                        this.Dataset.Context.Reader.Seek((long)secondaryBlock.DataBlockAddresses[dataBlockIndex], SeekOrigin.Begin);

                        var dataBlock = new ExtensibleArrayDataBlock(
                            this.Dataset.Context.Reader,
                            this.Dataset.Context.Superblock,
                            _header,
                            chunkSizeLength,
                            secondaryBlock.ElementsCount);

                        /* Set 'thing' info to refer to the data block */
                        var element = dataBlock.Elements[elementIndex];
                        return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
                    }
                }
            }
        }

        #endregion
    }
}

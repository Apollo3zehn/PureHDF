namespace HDF5.NET
{
    internal class H5D_Chunk4_ExtensibleArray : H5D_Chunk4
    {
        public H5D_Chunk4_ExtensibleArray(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
            base(dataset, layout, datasetAccess)
        {
            //
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            return ChunkInfo.None;


//            // H5Dearray.c (H5D__earray_idx_get_addr)

//            /* Check for unlimited dim. not being the slowest-changing dim. */
//            ulong chunkIndex;

//            var unlimitedDim = this.Dataset.Dataspace.DimensionMaxSizes
//                .ToList()
//                .FindLastIndex(value => value == H5Constants.Unlimited);

//            if (unlimitedDim > 0)
//            {
//                var swizzledCoords = new ulong[this.ChunkRank];

//                /* Compute coordinate offset from scaled offset */
//                for (int i = 0; i < this.ChunkRank; i++)
//                {
//                    swizzledCoords[i] = chunkIndices[i] * chunkDims[i];
//                }

//                this.SwizzleCoords(swizzledCoords, unlimitedDim);

//                /* Calculate the index of this chunk */
//                chunkIndex = swizzled_coords.ToLinearIndex(MaxDownChunks);
//            }
//            else
//            {
//                /* Calculate the index of this chunk */
//                chunkIndex = chunkIndices.ToLinearIndex(MaxDownChunks);
//            }

//            var chunkSizeLength = H5Utils.ComputeChunkSizeLength(chunkSize);
//            var header = new ExtensibleArrayHeader(this.Context.Reader, this.Context.Superblock, chunkSizeLength);
//            var indexBlock = header.IndexBlock;
//            var elementIndex = 0U;

//            var elements = new List<DataBlockElement>()
//                .AsEnumerable();

//            // elements
//            elements = elements.Concat(indexBlock.Elements);

//            // data blocks
//            ReadDataBlocks(indexBlock.DataBlockAddresses);

//            // secondary blocks
//#warning Is there any precalculated way to avoid checking all addresses?
//            var addresses = indexBlock
//                .SecondaryBlockAddresses
//                .Where(address => !this.Context.Superblock.IsUndefinedAddress(address));

//            foreach (var secondaryBlockAddress in addresses)
//            {
//                this.Context.Reader.Seek((long)secondaryBlockAddress, SeekOrigin.Begin);
//                var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(elementIndex + header.IndexBlockElementsCount);
//                var secondaryBlock = new ExtensibleArraySecondaryBlock(this.Context.Reader, this.Context.Superblock, header, secondaryBlockIndex);
//                ReadDataBlocks(secondaryBlock.DataBlockAddresses);
//            }

//            foreach (var element in elements)
//            {
//                // if page/element is initialized (see also datablock.PageBitmap)
//#warning Is there any precalculated way to avoid checking all addresses?
//                if (element.Address > 0 && !this.Context.Superblock.IsUndefinedAddress(element.Address))
//                    this.SeekAndReadChunk(buffer, element.ChunkSize, element.Address);
            }

//            void ReadDataBlocks(ulong[] dataBlockAddresses)
//            {
//#warning Is there any precalculated way to avoid checking all addresses?
//                dataBlockAddresses = dataBlockAddresses
//                    .Where(address => !this.Context.Superblock.IsUndefinedAddress(address))
//                    .ToArray();

//                foreach (var dataBlockAddress in dataBlockAddresses)
//                {
//                    this.Context.Reader.Seek((long)dataBlockAddress, SeekOrigin.Begin);
//                    var newElements = this.ReadExtensibleArrayDataBlock(header, chunkSizeLength, elementIndex);
//                    elements = elements.Concat(newElements);
//                    elementIndex += (uint)newElements.Length;
//                }
//            }
//        }

        //private DataBlockElement[] ReadExtensibleArrayDataBlock(ExtensibleArrayHeader header, uint chunkSizeLength, uint elementIndex)
        //{
        //    var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(elementIndex + header.IndexBlockElementsCount);
        //    var elementsCount = header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;
        //    var dataBlock = new ExtensibleArrayDataBlock(this.Context.Reader,
        //                                                 this.Context.Superblock,
        //                                                 header,
        //                                                 chunkSizeLength,
        //                                                 elementsCount);

        //    if (dataBlock.PageCount > 0)
        //    {
        //        var pages = new List<DataBlockPage>((int)dataBlock.PageCount);

        //        for (int i = 0; i < (int)dataBlock.PageCount; i++)
        //        {
        //            var page = new DataBlockPage(this.Context.Reader,
        //                                         this.Context.Superblock,
        //                                         header.DataBlockPageElementsCount,
        //                                         dataBlock.ClientID,
        //                                         chunkSizeLength);
        //            pages.Add(page);
        //        }

        //        return pages
        //            .SelectMany(page => page.Elements)
        //            .ToArray();
        //    }
        //    else
        //    {
        //        return dataBlock.Elements;
        //    }
        //}

        private void SwizzleCoords(ulong[] swizzledCoords, int unlimitedDim)
        {
            /* Nothing to do when unlimited dimension is at position 0 */
            if (unlimitedDim > 0)
            {
                var tmp = swizzledCoords[unlimitedDim];

                for (int i = unlimitedDim; i > 0; i++)
                {
                    swizzledCoords[i] = swizzledCoords[i - 1];
                }

                swizzledCoords[0] = tmp;
            }
        }
    }
}

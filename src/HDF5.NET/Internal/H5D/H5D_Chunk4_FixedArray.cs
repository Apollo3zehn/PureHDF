using System;
using System.IO;

namespace HDF5.NET
{
    internal class H5D_Chunk4_FixedArray : H5D_Chunk4
    {
        private FixedArrayHeader? _header;

        public H5D_Chunk4_FixedArray(H5Dataset dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) : 
            base(dataset, layout, datasetAccess)
        {
            //
        }

        protected override ChunkInfo GetChunkInfo(ulong[] chunkIndices)
        {
            // H5Dfarray.c (H5D__farray_idx_get_addr)

            /* Calculate the index of this chunk */
            var chunkIndex = chunkIndices.ToLinearIndex(this.DownMaxChunkCounts);

            /* Check for filters on chunks */
            if (this.Dataset.FilterPipeline is not null)
            {
                var chunkSizeLength = H5Utils.ComputeChunkSizeLength(this.ChunkByteSize);

                var element = this.GetElement(chunkIndex, reader =>
                {
                    return new FilteredDataBlockElement()
                    {
                        Address = this.Dataset.Context.Superblock.ReadOffset(reader),
                        ChunkSize = (uint)H5Utils.ReadUlong(reader, chunkSizeLength),
                        FilterMask = reader.ReadUInt32()
                    };
                });

                return element is not null
                    ? new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask)
                    : ChunkInfo.None;
            }
            else
            {
                var element = this.GetElement(chunkIndex, reader =>
                {
                    return new DataBlockElement()
                    {
                        Address = this.Dataset.Context.Superblock.ReadOffset(reader)
                    };
                });

                return element is not null
                    ? new ChunkInfo(element.Address, this.ChunkByteSize, 0)
                    : ChunkInfo.None;
            }
        }

        private T? GetElement<T>(ulong index, Func<H5BinaryReader, T> decode) where T : DataBlockElement
        {
            if (_header == null)
            {
                this.Dataset.Context.Reader.Seek((long)this.Dataset.DataLayout.Address, SeekOrigin.Begin);
                _header = new FixedArrayHeader(this.Dataset.Context.Reader, this.Dataset.Context.Superblock);
            }

            // H5FA.c (H5FA_get)

            /* Check if the fixed array data block has been allocated on disk yet */
            if (this.Dataset.Context.Superblock.IsUndefinedAddress(_header.DataBlockAddress))
            {
                /* Call the class's 'fill' callback */
                return null;
            }
            else
            {
                return this.LookupElement(index, decode);
            }
        }

        private T? LookupElement<T>(ulong index, Func<H5BinaryReader, T> decode) where T : DataBlockElement
        {
            // H5FA.c (H5FA_get)

            /* Get the data block */
            this.Dataset.Context.Reader.Seek((long)_header.DataBlockAddress, SeekOrigin.Begin);

            var dataBlock = new FixedArrayDataBlock<T>(
                this.Dataset.Context.Reader,
                this.Dataset.Context.Superblock,
                _header,
                decode);

            /* Check for paged data block */
            if (dataBlock.PageCount > 0)
            {
                /* Compute the page index */
                var pageIndex = index / dataBlock.ElementsPerPage;
                var pageBitmapEntry = dataBlock.PageBitmap[pageIndex / 8];
                var bitMaskIndex = (int)pageIndex % 8;

                /* Check if the page is defined yet */
                if ((pageBitmapEntry & H5Utils.SequentialBitMask[bitMaskIndex]) > 0)
                {
                    /* Compute the element index */
                    var elementIndex = index % dataBlock.ElementsPerPage;

                    /* Compute the address of the data block */
                    var pageSize = dataBlock.ElementsPerPage * _header.EntrySize + 4;
                    var pageAddress = this.Dataset.Context.Reader.BaseStream.Position + (long)(pageIndex * pageSize);

                    /* Check for using last page, to set the number of elements on the page */
                    ulong elementCount;

                    if (pageIndex + 1 == dataBlock.PageCount)
                        elementCount = dataBlock.LastPageElementCount;

                    else
                        elementCount = dataBlock.ElementsPerPage;

                    /* Protect the data block page */
                    this.Dataset.Context.Reader.Seek(pageAddress, SeekOrigin.Begin);

                    var page = new DataBlockPage<T>(
                        this.Dataset.Context.Reader,
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
}

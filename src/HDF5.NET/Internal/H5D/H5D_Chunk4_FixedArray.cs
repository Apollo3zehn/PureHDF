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

            var chunkSizeLength = H5Utils.ComputeChunkSizeLength(this.ChunkByteSize);

            if (_header == null)
            {
                this.Dataset.Context.Reader.Seek((long)this.Dataset.DataLayout.Address, SeekOrigin.Begin);
                _header = new FixedArrayHeader(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, chunkSizeLength);
            }

            /* Check if the fixed array data block has been allocated on disk yet */
            if (this.Dataset.Context.Superblock.IsUndefinedAddress(_header.DataBlockAddress))
            {
                /* Call the class's 'fill' callback */
                return ChunkInfo.None;
            }
            else
            {
                /* Get the data block */
                this.Dataset.Context.Reader.Seek((long)_header.DataBlockAddress, SeekOrigin.Begin);
                var dataBlock = new FixedArrayDataBlock(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, _header, chunkSizeLength);

                var chunkIndex = chunkIndices.ToLinearIndex(this.ScaledDims);

                /* Check for paged data block */
                if (dataBlock.PageCount > 0)
                {
                    /* Compute the page index */
                    var pageIndex = chunkIndex / dataBlock.ElementsPerPage;
                    var pageBitmapEntry = dataBlock.PageBitmap[pageIndex / 8];
                    var bitMaskIndex = (int)pageIndex % 8;

                    /* Check if the page is defined yet */
                    if ((pageBitmapEntry & H5Utils.SequentialBitMask[bitMaskIndex]) > 0)
                    {
                        /* Compute the element index */
                        var elementIndex = chunkIndex % dataBlock.ElementsPerPage;

                        /* Compute the address of the data block */
                        var pageSize =  dataBlock.ElementsPerPage * _header.EntrySize + 4;
                        var pageAddress = this.Dataset.Context.Reader.BaseStream.Position + (long)(pageIndex * pageSize);

                        /* Check for using last page, to set the number of elements on the page */
                        ulong elementCount;

                        if (pageIndex + 1 == dataBlock.PageCount)
                            elementCount = dataBlock.LastPageElementCount;

                        else
                            elementCount = dataBlock.ElementsPerPage;

                        /* Protect the data block page */
                        this.Dataset.Context.Reader.Seek(pageAddress, SeekOrigin.Begin);
                        var page = new DataBlockPage(this.Dataset.Context.Reader, this.Dataset.Context.Superblock, elementCount, dataBlock.ClientID, chunkSizeLength);

                        /* Retrieve element from data block */
                        var element = page.Elements[elementIndex];
                        return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
                    }
                    else
                    {
                        /* Call the class's 'fill' callback */
                        return ChunkInfo.None;
                    }
                }
                else
                {
                    /* Retrieve element from data block */
                    var element = dataBlock.Elements[chunkIndex];
                    return new ChunkInfo(element.Address, element.ChunkSize, element.FilterMask);
                }
            }
        }
    }
}

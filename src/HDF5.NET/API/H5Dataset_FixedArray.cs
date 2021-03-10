using System;
using System.IO;

namespace HDF5.NET
{
    public partial class H5Dataset
    {
        private void ReadFixedArray(Memory<byte> buffer, ulong chunkSize, ulong chunkIndex)
        {
            // H5Dfarray.c (H5D__farray_idx_get_addr)

            var chunkSizeLength = H5Utils.ComputeChunkSizeLength(chunkSize);
            var header = new FixedArrayHeader(this.Context.Reader, this.Context.Superblock, chunkSizeLength);

            /* Check if the fixed array data block has been allocated on disk yet */
            if (this.Context.Superblock.IsUndefinedAddress(header.DataBlockAddress))
            {
                /* Call the class's 'fill' callback */
                if (this.FillValue.IsDefined)
                    buffer.Span.Fill(this.FillValue.Value);

                return;
            }
            else
            {
                /* Get the data block */
                this.Context.Reader.Seek((long)header.DataBlockAddress, SeekOrigin.Begin);
                var dataBlock = new FixedArrayDataBlock(this.Context.Reader, this.Context.Superblock, header, chunkSizeLength);

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
                        var pageSize =  dataBlock.ElementsPerPage * header.EntrySize + 4;
                        var pageAddress = this.Context.Reader.BaseStream.Position + (long)(pageIndex * pageSize);

                        /* Check for using last page, to set the number of elements on the page */
                        ulong elementCount;

                        if (pageIndex + 1 == dataBlock.PageCount)
                            elementCount = dataBlock.LastPageElementCount;

                        else
                            elementCount = dataBlock.ElementsPerPage;

                        /* Protect the data block page */
                        this.Context.Reader.Seek(pageAddress, SeekOrigin.Begin);
                        var page = new DataBlockPage(this.Context.Reader, this.Context.Superblock, elementCount, dataBlock.ClientID, chunkSizeLength);

                        /* Retrieve element from data block */
                        var element = page.Elements[elementIndex];
                        this.SeekAndReadChunk(buffer, element.ChunkSize, element.FilterMask, element.Address);
                    }
                    else
                    {
                        /* Call the class's 'fill' callback */
                        if (this.FillValue.IsDefined)
                            buffer.Span.Fill(this.FillValue.Value);

                        return;
                    }
                }
                else
                {
                    /* Retrieve element from data block */
                    var element = dataBlock.Elements[chunkIndex];
                    this.SeekAndReadChunk(buffer, element.ChunkSize, element.FilterMask, element.Address);
                }
            }
        }
    }
}

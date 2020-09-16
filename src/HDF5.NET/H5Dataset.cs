using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{DataType.Class}'")]
    public class H5Dataset : H5AttributableLink
    {
        #region Constructors

        internal H5Dataset(H5File file, string name, ObjectHeader objectHeader) 
            : base(file, name, objectHeader)
        {
            foreach (var message in this.ObjectHeader.HeaderMessages)
            {
                var type = message.Data.GetType();

                if (typeof(DataLayoutMessage).IsAssignableFrom(type))
                    this.DataLayout = (DataLayoutMessage)message.Data;

                else if (type == typeof(DataspaceMessage))
                    this.Dataspace = (DataspaceMessage)message.Data;

                else if (type == typeof(DatatypeMessage))
                    this.DataType = (DatatypeMessage)message.Data;

                else if (type == typeof(FillValueMessage))
                    this.FillValue = (FillValueMessage)message.Data;

                else if (type == typeof(ObjectModificationMessage))
                    this.ObjectModification = (ObjectModificationMessage)message.Data;
            }
        }

        #endregion

        #region Properties

        public DataLayoutMessage DataLayout { get; }

        public DataspaceMessage Dataspace { get; }

        public DatatypeMessage DataType { get; }

        public FillValueMessage FillValue { get; }

        public ObjectModificationMessage ObjectModification { get; }

        #endregion

        #region Methods

        public T[] Read<T>() where T : unmanaged
        {
            switch (this.DataLayout.LayoutClass)
            {
                // Compact: The array is stored in one contiguous block as part of
                // this object header message.
                case LayoutClass.Compact:
                    return this.ReadCompact<T>();

                // Contiguous: The array is stored in one contiguous area of the file. 
                // This layout requires that the size of the array be constant: 
                // data manipulations such as chunking, compression, checksums, 
                // or encryption are not permitted. The message stores the total
                // storage size of the array. The offset of an element from the 
                // beginning of the storage area is computed as in a C array.
                case LayoutClass.Contiguous:
                    return this.ReadContiguous<T>();

                // Chunked: The array domain is regularly decomposed into chunks,
                // and each chunk is allocated and stored separately. This layout 
                // supports arbitrary element traversals, compression, encryption,
                // and checksums (these features are described in other messages).
                // The message stores the size of a chunk instead of the size of the
                // entire array; the storage size of the entire array can be 
                // calculated by traversing the chunk index that stores the chunk 
                // addresses.
                case LayoutClass.Chunked:
                    return this.ReadChunked<T>();

                // Virtual: This is only supported for version 4 of the Data Layout 
                // message. The message stores information that is used to locate 
                // the global heap collection containing the Virtual Dataset (VDS) 
                // mapping information. The mapping associates the VDS to the source
                // dataset elements that are stored across a collection of HDF5 files.
                case LayoutClass.VirtualStorage:
                    throw new NotImplementedException();

                default:
                    throw new Exception($"The data layout class '{this.DataLayout.LayoutClass}' is not supported.");
            }
        }

        private T[] ReadCompact<T>() where T : unmanaged
        {
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
#warning untested
                return MemoryMarshal
                    .Cast<byte, T>(layout12.CompactData)
                    .ToArray();
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;

                return MemoryMarshal
                    .Cast<byte, T>(compact.RawData)
                    .ToArray();
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        private T[] ReadContiguous<T>() where T : unmanaged
        {
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
#warning untested
                var buffer = this.GetBuffer<T>(out var result);

                // read data
                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                this.File.Reader.Read(buffer);

                return result;
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var contiguous = (ContiguousStoragePropertyDescription)layout34.Properties;
                var buffer = this.GetBuffer<T>(out var result);

                // read data
                this.File.Reader.Seek((long)contiguous.Address, SeekOrigin.Begin);
                this.File.Reader.Read(buffer);

                return result;
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        private T[] ReadChunked<T>() where T : unmanaged
        {
            var buffer = this.GetBuffer<T>(out var result);

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                if (this.File.Superblock.IsUndefinedAddress(layout12.DataAddress))
                    return result;

                this.File.Reader.Seek((int)layout12.DataAddress, SeekOrigin.Begin);
                this.ReadChunkedBTree1(buffer, layout12.Dimensionality);
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;
                var chunkSize = (int)this.CalculateByteSize(chunked4.DimensionSizes);

                if (this.File.Superblock.IsUndefinedAddress(chunked4.Address))
                    return result;

                this.File.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);

                switch (chunked4.ChunkIndexingType)
                {
                    // the current, maximum, and chunk dimension sizes are all the same
                    case ChunkIndexingType.SingleChunk:
                        var singleChunkInfo = (SingleChunkIndexingInformation)chunked4.IndexingTypeInformation;
                        this.File.Reader.Read(buffer);
                        break;

                    // fixed maximum dimension sizes
                    // no filter applied to the dataset
                    // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                    case ChunkIndexingType.Implicit:
                        var @implicitInfo = (ImplicitIndexingInformation)chunked4.IndexingTypeInformation;
                        this.File.Reader.Read(buffer);
                        break;

                    // fixed maximum dimension sizes
                    case ChunkIndexingType.FixedArray:
                        var fixedArrayInfo = (FixedArrayIndexingInformation)chunked4.IndexingTypeInformation;
                        this.ReadFixedArray(buffer, chunkSize);
                        break;

                    // only one dimension of unlimited extent
                    case ChunkIndexingType.ExtensibleArray:
                        var extensibleArrayInfo = (ExtensibleArrayIndexingInformation)chunked4.IndexingTypeInformation;
                        throw new NotImplementedException();
                        break;

                    // more than one dimension of unlimited extent
                    case ChunkIndexingType.BTree2:
                        var btree2Info = (BTree2IndexingInformation)chunked4.IndexingTypeInformation;
                        this.ReadChunkedBTree2(buffer, chunkSize);
                        break;

                    default:
                        break;
                }
            }
            else if (this.DataLayout is DataLayoutMessage3 layout3)
            {
                var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;
                this.File.Reader.Seek((int)chunked3.Address, SeekOrigin.Begin);
                this.ReadChunkedBTree1(buffer, (byte)(chunked3.Dimensionality - 1));
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            return result;
        }

        private void ReadChunkedBTree1(Span<byte> buffer, byte dimensionality)
        {
            // btree1
            Func<BTree1RawDataChunksKey> decodeKey = () => this.DecodeRawDataChunksKey(dimensionality);
            var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock, decodeKey);
            var leafKeys = btree1.EnumerateNodes().ToList();
            var childAddresses = leafKeys.SelectMany(key => key.ChildAddresses).ToArray();
            var keys = leafKeys.SelectMany(key => key.Keys).ToArray();

            // read data
            var offset = 0;
            var chunkCount = (ulong)childAddresses.Length;

            for (ulong i = 0; i < chunkCount; i++)
            {
                var chunkSize = (int)keys[i].ChunkSize;
                this.File.Reader.Seek((long)childAddresses[i], SeekOrigin.Begin);
                this.File.Reader.Read(buffer.Slice(offset, chunkSize));
                offset += chunkSize;
            }
        }

        private void ReadChunkedBTree2(Span<byte> buffer, int chunkSize)
        {
            // btree2
            var btree2 = new BTree2Header<BTree2Record10>(this.File.Reader, this.File.Superblock);
            var records = btree2.EnumerateRecords();

            // read data
            var offset = 0;

            foreach (var record in records)
            {
                var length = Math.Min(chunkSize, buffer.Length - offset);
                this.File.Reader.Seek((long)record.Address, SeekOrigin.Begin);
                this.File.Reader.Read(buffer.Slice(offset, length));
                offset += chunkSize;
            }
        }

        private void ReadFixedArray(Span<byte> buffer, int chunkSize)
        {
            // H5Dfarray.c (H5D__farray_crt_context)
            /* Compute the size required for encoding the size of a chunk, allowing
             *      for an extra byte, in case the filter makes the chunk larger.
             */
            var chunkSizeLength = 1 + ((uint)Math.Log(chunkSize, 2) + 8) / 8;

            if (chunkSizeLength > 8)
                chunkSizeLength = 8;

            // go
            var fixedArray = new FixedArrayHeader(this.File.Reader, this.File.Superblock, chunkSizeLength);
            var datablock = fixedArray.DataBlock;

            IEnumerator<FixedArrayDataBlockElement> elements;

            if (datablock.PageCount > 0)
            {
                var pages = new List<FixedArrayDataBlockPage>((int)datablock.PageCount);

                for (int i = 0; i < (int)datablock.PageCount; i++)
                {
                    var page = new FixedArrayDataBlockPage(this.File.Reader, this.File.Superblock, datablock.ElementsPerPage, datablock.ClientID, chunkSizeLength);
                    pages.Add(page);
                }

                elements = pages
                    .SelectMany(page => page.Elements)
                    .GetEnumerator();
            }
            else
            {
                elements = datablock.Elements
                    .AsEnumerable()
                    .GetEnumerator();
            }

            var offset = 0;
            var index = 0UL;

            for (ulong i = 0; i < fixedArray.EntriesCount; i++)
            {
                elements.MoveNext();
                var element = elements.Current;

                // if page is initialized (see also datablock.PageBitmap)
                if (element.Address > 0)
                {
                    this.File.Reader.Seek((long)element.Address, SeekOrigin.Begin);
                    var length = Math.Min(chunkSize, buffer.Length - offset);
                    var currentBuffer = buffer.Slice(offset, length);
                    this.File.Reader.Read(currentBuffer);
                }
               
                offset += chunkSize;
                index++;
            }
        }

        private Span<byte> GetBuffer<T>(out T[] result) where T : unmanaged
        {
            // first, get byte size
            var byteSize = this.CalculateByteSize(this.Dataspace.DimensionSizes) * this.DataType.Size;

            // second, convert file type (e.g. 2 bytes) to T (e.g. 4 bytes)
            var arraySize = byteSize / (ulong)Unsafe.SizeOf<T>();

            // finally, create buffer
            result = new T[arraySize];
            var buffer = MemoryMarshal.AsBytes(result.AsSpan());

            return buffer;
        }

        private ulong CalculateByteSize(ulong[] dimensionSizes)
        {
            var byteSize = 0UL;

            if (dimensionSizes.Any())
                byteSize = dimensionSizes.Aggregate((x, y) => x * y);

            return byteSize;
        }

        #endregion

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree1RawDataChunksKey DecodeRawDataChunksKey(byte dimensionality)
        {
            return new BTree1RawDataChunksKey(this.File.Reader, dimensionality);
        }

        #endregion
    }
}

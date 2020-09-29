using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Datatype.Class}'")]
    public class H5Dataset : H5AttributableLink, IDataContainer
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
                    this.Datatype = (DatatypeMessage)message.Data;

                else if (type == typeof(FillValueMessage))
                    this.FillValue = (FillValueMessage)message.Data;

                else if (type == typeof(FilterPipelineMessage))
                    this.FilterPipeline = (FilterPipelineMessage)message.Data;

                else if (type == typeof(ObjectModificationMessage))
                    this.ObjectModification = (ObjectModificationMessage)message.Data;
            }

            // check that required fields are set
            if (this.DataLayout == null)
                throw new Exception("The data layout message is missing.");

            if (this.Dataspace == null)
                throw new Exception("The dataspace message is missing.");

            if (this.Datatype == null)
                throw new Exception("The data type message is missing.");

            if (this.FillValue == null)
                throw new Exception("The fill value message is missing.");
        }

        #endregion

        #region Properties

        public DataLayoutMessage DataLayout { get; } = null!;

        public DataspaceMessage Dataspace { get; } = null!;

        public DatatypeMessage Datatype { get; } = null!;

        public FillValueMessage FillValue { get; } = null!;

        public FilterPipelineMessage? FilterPipeline { get; }

        public ObjectModificationMessage? ObjectModification { get; }

        #endregion

        #region Methods

        public T[] Read<T>() where T : struct
        {
            return this.Read<T>(skipShuffle: false);
        }

        public T[] ReadCompound<T>() where T : struct
        {
            return this.ReadCompound<T>(fieldInfo => fieldInfo.Name);
        }

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string> getName) where T : struct
        {
            var data = this.Read<byte>();

            return H5Utils.ReadCompound<T>(this.Datatype, this.Dataspace, this.File.Superblock, data, getName);
        }

        public string[] ReadString()
        {
            var data = this.Read<byte>(skipTypeCheck: true);
            return H5Utils.ReadString(this.Datatype, data, this.File.Superblock);
        }

        internal T[] Read<T>(bool skipTypeCheck = false, bool skipShuffle = false) where T : struct
        {
            if (!skipTypeCheck)
            {
                switch (this.Datatype.Class)
                {
                    case DatatypeMessageClass.FixedPoint:
                    case DatatypeMessageClass.FloatingPoint:
                    case DatatypeMessageClass.BitField:
                    case DatatypeMessageClass.Opaque:
                    case DatatypeMessageClass.Compound:
                    case DatatypeMessageClass.Enumerated:
                    case DatatypeMessageClass.Array:
                        break;

                    default:
                        throw new Exception($"This method can only be used with one of the following type classes: '{DatatypeMessageClass.FixedPoint}', '{DatatypeMessageClass.FloatingPoint}', '{DatatypeMessageClass.BitField}', '{DatatypeMessageClass.Opaque}', '{DatatypeMessageClass.Compound}', '{DatatypeMessageClass.Enumerated}' and '{DatatypeMessageClass.Array}'.");
                }
            }

            // for testing only
            if (skipShuffle && this.FilterPipeline != null)
            {
                var filtersToRemove = this
                    .FilterPipeline
                    .FilterDescriptions
                    .Where(description => description.Identifier == FilterIdentifier.Shuffle)
                    .ToList();

                foreach (var filter in filtersToRemove)
                {
                    this.FilterPipeline.FilterDescriptions.Remove(filter);
                }
            }

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

        private T[] ReadCompact<T>() where T : struct
        {
            byte[] buffer;

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
#warning untested
                buffer = layout12.CompactData;
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                buffer = compact.RawData;
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            var result = MemoryMarshal
                    .Cast<byte, T>(buffer);

            this.EnsureEndianness(buffer);
            return result.ToArray();
        }

        private T[] ReadContiguous<T>() where T : struct
        {
            ulong address;

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                address = layout12.DataAddress;
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var contiguous = (ContiguousStoragePropertyDescription)layout34.Properties;
                address = contiguous.Address;
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            // read data
            var buffer = this.GetBuffer<T>(out var result);

            if (this.File.Superblock.IsUndefinedAddress(address))
            {
                if (this.FillValue.IsDefined)
                    buffer.Fill(this.FillValue.Value);
            }
            else
            {
                this.File.Reader.Seek((long)address, SeekOrigin.Begin);
                this.File.Reader.Read(buffer);
            }

            this.EnsureEndianness(buffer);
            return result;
        }

        private T[] ReadChunked<T>() where T : struct
        {
            var buffer = this.GetBuffer<T>(out var result);

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                if (this.File.Superblock.IsUndefinedAddress(layout12.DataAddress))
                {
                    if (this.FillValue.IsDefined)
                        buffer.Fill(this.FillValue.Value);
                }
                else
                {
                    var chunkSize = this.CalculateByteSize(layout12.DimensionSizes);
                    this.File.Reader.Seek((int)layout12.DataAddress, SeekOrigin.Begin);
                    this.ReadChunkedBTree1(buffer, layout12.Dimensionality, chunkSize);
                }                
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;
                var chunkSize = this.CalculateByteSize(chunked4.DimensionSizes);

                if (this.File.Superblock.IsUndefinedAddress(chunked4.Address))
                {
                    if (this.FillValue.IsDefined)
                        buffer.Fill(this.FillValue.Value);
                }
                else
                {
                    this.File.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);

                    switch (chunked4.ChunkIndexingType)
                    {
                        // the current, maximum, and chunk dimension sizes are all the same
                        case ChunkIndexingType.SingleChunk:
                            var singleChunkInfo = (SingleChunkIndexingInformation)chunked4.IndexingTypeInformation;
                            this.ReadChunk(buffer, chunkSize);
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
                            this.ReadExtensibleArray(buffer, chunkSize);
                            break;

                        // more than one dimension of unlimited extent
                        case ChunkIndexingType.BTree2:
                            var btree2Info = (BTree2IndexingInformation)chunked4.IndexingTypeInformation;
                            this.ReadChunkedBTree2(buffer, (byte)(chunked4.Dimensionality - 1), chunkSize);
                            break;

                        default:
                            break;
                    }
                }
            }
            else if (this.DataLayout is DataLayoutMessage3 layout3)
            {
                var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;

                if (this.File.Superblock.IsUndefinedAddress(chunked3.Address))
                {
                    if (this.FillValue.IsDefined)
                        buffer.Fill(this.FillValue.Value);
                }
                else
                {
                    var chunkSize = this.CalculateByteSize(chunked3.DimensionSizes);
                    this.File.Reader.Seek((int)chunked3.Address, SeekOrigin.Begin);
                    this.ReadChunkedBTree1(buffer, (byte)(chunked3.Dimensionality - 1), chunkSize);
                }
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            this.EnsureEndianness(buffer);
            return result;
        }

        private void ReadChunkedBTree1(Span<byte> buffer, byte dimensionality, ulong chunkSize)
        {
            // btree1
            Func<BTree1RawDataChunksKey> decodeKey = () => this.DecodeRawDataChunksKey(dimensionality);
            var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock, decodeKey);
            var nodes = btree1.EnumerateNodes().ToList();
            var childAddresses = nodes.SelectMany(key => key.ChildAddresses).ToArray();
            var keys = nodes.SelectMany(key => key.Keys).ToArray();

            // read data
            var offset = 0UL;
            var chunkCount = (ulong)childAddresses.Length;

            for (ulong i = 0; i < chunkCount; i++)
            {
                var rawChunkSize = keys[i].ChunkSize;
                this.SeekSliceAndReadChunk(offset, chunkSize, rawChunkSize, childAddresses[i], buffer);
                offset += rawChunkSize;
            }
        }

        private void ReadChunkedBTree2(Span<byte> buffer, byte dimensionality, ulong chunkSize)
        {
            var offset = 0UL;

            if (this.FilterPipeline == null)
            {
                // btree2
                Func<BTree2Record10> decodeKey = () => this.DecodeRecord10(dimensionality);
                var btree2 = new BTree2Header<BTree2Record10>(this.File.Reader, this.File.Superblock, decodeKey);
                var records = btree2
                    .EnumerateRecords()
                    .ToList();

                // read data
                foreach (var record in records)
                {
                    this.SeekSliceAndReadChunk(offset, chunkSize, chunkSize, record.Address, buffer);
                    offset += chunkSize;
                }
            }
            else
            {
                // btree2
                var chunkSizeLength = this.ComputeChunkSizeLength(chunkSize);
                Func<BTree2Record11> decodeKey = () => this.DecodeRecord11(dimensionality, chunkSizeLength);
                var btree2 = new BTree2Header<BTree2Record11>(this.File.Reader, this.File.Superblock, decodeKey);
                var records = btree2
                    .EnumerateRecords()
                    .ToList();

                // read data
                foreach (var record in records)
                {
                    this.SeekSliceAndReadChunk(offset, record.ChunkSize, record.ChunkSize, record.Address, buffer);
                    offset += chunkSize;
                }
            }
        }

        private void ReadFixedArray(Span<byte> buffer, ulong chunkSize)
        {
            var chunkSizeLength = this.ComputeChunkSizeLength(chunkSize);
            var header = new FixedArrayHeader(this.File.Reader, this.File.Superblock, chunkSizeLength);
            var dataBlock = header.DataBlock;

            IEnumerable<DataBlockElement> elements;

            if (dataBlock.PageCount > 0)
            {
                var pages = new List<DataBlockPage>((int)dataBlock.PageCount);

                for (int i = 0; i < (int)dataBlock.PageCount; i++)
                {
                    var page = new DataBlockPage(this.File.Reader, this.File.Superblock, dataBlock.ElementsPerPage, dataBlock.ClientID, chunkSizeLength);
                    pages.Add(page);
                }

                elements = pages.SelectMany(page => page.Elements);
            }
            else
            {
                elements = dataBlock.Elements.AsEnumerable();
            }

            var offset = 0UL;
            var index = 0UL;
            var enumerator = elements.GetEnumerator();

            for (ulong i = 0; i < header.EntriesCount; i++)
            {
                enumerator.MoveNext();
                var element = enumerator.Current;

                // if page/element is initialized (see also datablock.PageBitmap)
                if (element.Address > 0)
                    this.SeekSliceAndReadChunk(offset, chunkSize, element.ChunkSize, element.Address, buffer);
               
                offset += chunkSize;
                index++;
            }
        }

        // for later: H5EA__lookup_elmt

        private void ReadExtensibleArray(Span<byte> buffer, ulong chunkSize)
        {
            var chunkSizeLength = this.ComputeChunkSizeLength(chunkSize);
            var header = new ExtensibleArrayHeader(this.File.Reader, this.File.Superblock, chunkSizeLength);
            var indexBlock = header.IndexBlock;
            var elementIndex = 0U;

            var elements = new List<DataBlockElement>()
                .AsEnumerable();

            // elements
            elements = elements.Concat(indexBlock.Elements);

            // data blocks
            ReadDataBlocks(indexBlock.DataBlockAddresses);

            // secondary blocks
#warning Is there any precalculated way to avoid checking all addresses?
            var addresses = indexBlock
                .SecondaryBlockAddresses
                .Where(address => !this.File.Superblock.IsUndefinedAddress(address));

            foreach (var secondaryBlockAddress in addresses)
            {
                this.File.Reader.Seek((long)secondaryBlockAddress, SeekOrigin.Begin);
                var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(elementIndex + header.IndexBlockElementsCount);
                var secondaryBlock = new ExtensibleArraySecondaryBlock(this.File.Reader, this.File.Superblock, header, secondaryBlockIndex);
                ReadDataBlocks(secondaryBlock.DataBlockAddresses);
            }

            var offset = 0UL;

            foreach (var element in elements)
            {
                // if page/element is initialized (see also datablock.PageBitmap)
#warning Is there any precalculated way to avoid checking all addresses?
                if (element.Address > 0 && !this.File.Superblock.IsUndefinedAddress(element.Address))
                    this.SeekSliceAndReadChunk(offset, chunkSize, element.ChunkSize, element.Address, buffer);

                offset += chunkSize;
            }

            void ReadDataBlocks(ulong[] dataBlockAddresses)
            {
#warning Is there any precalculated way to avoid checking all addresses?
                dataBlockAddresses = dataBlockAddresses
                    .Where(address => !this.File.Superblock.IsUndefinedAddress(address))
                    .ToArray();

                foreach (var dataBlockAddress in dataBlockAddresses)
                {
                    this.File.Reader.Seek((long)dataBlockAddress, SeekOrigin.Begin);
                    var newElements = this.ReadExtensibleArrayDataBlock(header, chunkSizeLength, elementIndex);
                    elements = elements.Concat(newElements);
                    elementIndex += (uint)newElements.Length;
                }
            }
        }

        private DataBlockElement[] ReadExtensibleArrayDataBlock(ExtensibleArrayHeader header, uint chunkSizeLength, uint elementIndex)
        {
            var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(elementIndex + header.IndexBlockElementsCount);
            var elementsCount = header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;
            var dataBlock = new ExtensibleArrayDataBlock(this.File.Reader,
                                                         this.File.Superblock,
                                                         header,
                                                         chunkSizeLength,
                                                         elementsCount);

            if (dataBlock.PageCount > 0)
            {
                var pages = new List<DataBlockPage>((int)dataBlock.PageCount);

                for (int i = 0; i < (int)dataBlock.PageCount; i++)
                {
                    var page = new DataBlockPage(this.File.Reader,
                                                 this.File.Superblock,
                                                 header.DataBlockPageElementsCount,
                                                 dataBlock.ClientID,
                                                 chunkSizeLength);
                    pages.Add(page);
                }

                return pages
                    .SelectMany(page => page.Elements)
                    .ToArray();
            }
            else
            {
                return dataBlock.Elements;
            }
        }

        private Span<byte> GetBuffer<T>(out T[] result) where T : struct
        {
            // first, get byte size
            var byteSize = this.CalculateByteSize(this.Dataspace.DimensionSizes) * this.Datatype.Size;

            // second, convert file type (e.g. 2 bytes) to T (e.g. 4 bytes)
            var arraySize = byteSize / (ulong)Unsafe.SizeOf<T>();

            // finally, create buffer
            result = new T[arraySize];
            var buffer = MemoryMarshal.AsBytes(result.AsSpan());

            return buffer;
        }

        private ulong CalculateByteSize(uint[] dimensionSizes)
        {
            var byteSize = 0UL;

            if (dimensionSizes.Any())
                byteSize = dimensionSizes.Aggregate((x, y) => x * y);

            return byteSize;
        }

        private ulong CalculateByteSize(ulong[] dimensionSizes)
        {
            var byteSize = 0UL;

            if (dimensionSizes.Any())
                byteSize = dimensionSizes.Aggregate((x, y) => x * y);

            return byteSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SeekSliceAndReadChunk(ulong offset, ulong chunkSize, ulong rawChunkSize, ulong address, Span<byte> buffer)
        {
            if (this.File.Superblock.IsUndefinedAddress(address))
            {
                buffer.Fill(this.FillValue.Value);
            }
            else
            {
                this.File.Reader.Seek((long)address, SeekOrigin.Begin);
                var length = Math.Min(chunkSize, (ulong)buffer.Length - offset);
                // https://github.com/dotnet/apireviews/tree/main/2016/11-04-SpanOfT#spant-and-64-bit
                var bufferSlice = buffer.Slice((int)offset, (int)length);
                this.ReadChunk(bufferSlice, rawChunkSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadChunk(Span<byte> buffer, ulong rawChunkSize)
        {
            if (this.FilterPipeline == null)
            {
                this.File.Reader.Read(buffer);
            }
            else
            {
                using var filterBufferOwner = MemoryPool<byte>.Shared.Rent((int)rawChunkSize);
                var filterBuffer = filterBufferOwner.Memory[0..(int)rawChunkSize];
                this.File.Reader.Read(filterBuffer.Span);

                H5Filter.ExecutePipeline(this.FilterPipeline.FilterDescriptions, ExtendedFilterFlags.Reverse, filterBuffer, buffer);
            }
        }

        private uint ComputeChunkSizeLength(ulong chunkSize)
        {
            // H5Dearray.c (H5D__earray_crt_context)
            /* Compute the size required for encoding the size of a chunk, allowing
             *      for an extra byte, in case the filter makes the chunk larger.
             */
            var chunkSizeLength = 1 + ((uint)Math.Log(chunkSize, 2) + 8) / 8;

            if (chunkSizeLength > 8)
                chunkSizeLength = 8;

            return chunkSizeLength;
        }

        private void EnsureEndianness(Span<byte> buffer)
        {
            var byteOrderAware = this.Datatype.BitField as IByteOrderAware;

            if (byteOrderAware != null)
                H5Utils.EnsureEndianness(buffer.ToArray(), buffer, byteOrderAware.ByteOrder, this.Datatype.Size);
        }

        #endregion

        #region Callbacks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree1RawDataChunksKey DecodeRawDataChunksKey(byte dimensionality)
        {
            return new BTree1RawDataChunksKey(this.File.Reader, dimensionality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record10 DecodeRecord10(byte dimensionality)
        {
            return new BTree2Record10(this.File.Reader, this.File.Superblock, dimensionality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BTree2Record11 DecodeRecord11(byte dimensionality, uint chunkSizeLength)
        {
            return new BTree2Record11(this.File.Reader, this.File.Superblock, dimensionality, chunkSizeLength);
        }

        #endregion
    }
}

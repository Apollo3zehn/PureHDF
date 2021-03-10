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
    public partial class H5Dataset : H5AttributableObject
    {
        #region Fields

        private H5File _file;
        private IChunkCache _chunkCache;

        #endregion

        #region Constructors

        internal H5Dataset(H5File file, H5Context context, H5NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            _file = file;

            foreach (var message in this.Header.HeaderMessages)
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

                else if (type == typeof(ExternalFileListMessage))
                    this.ExternalFileList = (ExternalFileListMessage)message.Data;
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

        public ExternalFileListMessage? ExternalFileList { get; }

        #endregion

        #region Public

        public byte[] Read(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = this.Read<byte>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        public T[] Read<T>(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            var result = this.Read<T>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        public void Read<T>(
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            this.Read(
                buffer,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);
        }

        public T[] ReadCompound<T>(H5DatasetAccess datasetAccess = default)
            where T : struct
        {
            return this.ReadCompound<T>(fieldInfo => fieldInfo.Name, datasetAccess);
        }

//#error Add missing APIs.

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string> getName, H5DatasetAccess datasetAccess = default)
            where T : struct
        {
            var data = this.Read<byte>(datasetAccess: datasetAccess);
            return H5Utils.ReadCompound<T>(this.Datatype, this.Dataspace, this.Context.Superblock, data, getName);
        }

        public string[] ReadString(H5DatasetAccess datasetAccess = default)
        {
            var data = this.Read<byte>(null, datasetAccess: datasetAccess, skipTypeCheck: true);
            return H5Utils.ReadString(this.Datatype, data, this.Context.Superblock);
        }

        #endregion

        #region Private

#warning use implicit cast operator for multi dim arrays? http://dontcodetired.com/blog/post/Writing-Implicit-and-Explicit-C-Conversion-Operators

        internal T[]? Read<T>(
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default,
            bool skipTypeCheck = false,
            bool skipShuffle = false) where T : unmanaged
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
                    case DatatypeMessageClass.Reference:
                    case DatatypeMessageClass.Enumerated:
                    case DatatypeMessageClass.Array:
                        break;

                    default:
                        throw new Exception($"This method can only be used with one of the following type classes: '{DatatypeMessageClass.FixedPoint}', '{DatatypeMessageClass.FloatingPoint}', '{DatatypeMessageClass.BitField}', '{DatatypeMessageClass.Opaque}', '{DatatypeMessageClass.Compound}', '{DatatypeMessageClass.Reference}', '{DatatypeMessageClass.Enumerated}' and '{DatatypeMessageClass.Array}'.");
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

            /* dims & file selection */
            var datasetDims = default(ulong[]);
            var result = default(T[]);

            switch (this.Dataspace.Type)
            {
                case DataspaceType.Scalar:

                    datasetDims = new ulong[] { 1 };

                    if (fileSelection is null)
                        fileSelection = HyperslabSelection.Scalar();

                    break;

                case DataspaceType.Simple:

                    datasetDims = this.Dataspace.DimensionSizes;

                    if (fileSelection is null)
                        fileSelection = HyperslabSelection.All(datasetDims);

                    break;

                case DataspaceType.Null:
                    return new T[0];

                default:
                    throw new Exception($"Unsupported data space type '{this.Dataspace.Type}'.");
            }

            /* chunk dims */
            var datasetChunkDims = this.DataLayout.LayoutClass == LayoutClass.Chunked
                ? this.GetChunkDims()
                : datasetDims;

            /* result buffer */
            var totalCount = fileSelection.GetTotalCount();

            if (buffer.Equals(default))
                buffer = this.GetBuffer(totalCount, out result);

            /* memory selection */
            if (memorySelection is null)
                memorySelection = new HyperslabSelection(start: 0, block: totalCount);

            /* check both selections */
            var fileHyperslabSelection = fileSelection as HyperslabSelection;
            var memoryHyperslabSelection = memorySelection as HyperslabSelection;

            if (fileHyperslabSelection == null || memoryHyperslabSelection == null)
                throw new NotSupportedException("Only hyperslab selections are currently supported.");

            /* memory dims */
            if (memoryDims is null)
                memoryDims = new ulong[] { totalCount };

            /* buffer provider */
            Func<ulong[], Memory<byte>>? getSourceBuffer = this.DataLayout.LayoutClass switch
            {
                /* Compact: The array is stored in one contiguous block as part of
                 * this object header message. 
                 */
                LayoutClass.Compact => indices =>
                    this.ReadCompact(),

                /* Chunked: The array domain is regularly decomposed into chunks,
                 * and each chunk is allocated and stored separately. This layout 
                 * supports arbitrary element traversals, compression, encryption,
                 * and checksums (these features are described in other messages).
                 * The message stores the size of a chunk instead of the size of the
                 * entire array; the storage size of the entire array can be 
                 * calculated by traversing the chunk index that stores the chunk 
                 * addresses. 
                 */
                LayoutClass.Chunked => indices =>
                    this.GetChunkCache(datasetAccess).GetChunk(indices, () => this.ReadChunk(indices)),

                /* Virtual: This is only supported for version 4 of the Data Layout 
                 * message. The message stores information that is used to locate 
                 * the global heap collection containing the Virtual Dataset (VDS) 
                 * mapping information. The mapping associates the VDS to the source
                 * dataset elements that are stored across a collection of HDF5 files.
                 */
                LayoutClass.VirtualStorage => throw new NotImplementedException(),

                /* default */
                _ => null
            };

            var sourceStream = this.DataLayout.LayoutClass switch
            {
                /* Contiguous: The array is stored in one contiguous area of the file. 
                 * This layout requires that the size of the array be constant: 
                 * data manipulations such as chunking, compression, checksums, 
                 * or encryption are not permitted. The message stores the total
                 * storage size of the array. The offset of an element from the 
                 * beginning of the storage area is computed as in a C array.
                 */
                LayoutClass.Contiguous => this.ReadContiguousAsStream(datasetAccess),

                /* default */
                _ => null
            };

            Func<ulong[], Stream>? getSourceStream = sourceStream is not null
                ? indices => sourceStream
                : null;

            try
            {
                if (getSourceBuffer is null && getSourceStream is null)
                    new Exception($"The data layout class '{this.DataLayout.LayoutClass}' is not supported.");

                /* copy info */
                var copyInfo = new CopyInfo(
                    datasetDims,
                    datasetChunkDims,
                    memoryDims,
                    memoryDims,
                    fileHyperslabSelection,
                    memoryHyperslabSelection,
                    GetSourceBuffer: getSourceBuffer,
                    GetSourceStream: getSourceStream,
                    GetTargetBuffer: indices => buffer.Cast<T, byte>(),
                    TypeSize: (int)this.Datatype.Size
                );

                HyperslabUtils.Copy(fileHyperslabSelection.Rank, memoryHyperslabSelection.Rank, copyInfo);

                return result;
            }
            finally
            {
                sourceStream?.Dispose();
            }
        }

#warning Use this instead of SpanExtensions!
        // https://docs.microsoft.com/en-us/dotnet/api/system.array?view=netcore-3.1
        // max array length is 0X7FEFFFFF = int.MaxValue - 1024^2 bytes
        // max multi dim array length seems to be 0X7FEFFFFF x 2, but no confirmation found
        private unsafe T ReadCompactMultiDim<T>()
        {
            // vllt. einfach eine zweite Read<T> Methode (z.B. ReadMultiDim), 
            // die keine generic constraint hat (leider), aber T zuerst auf IsArray
            // geprüft wird
            // beide Methoden definieren dann ein Lambda, um den Buffer entsprechender
            // Größe zu erzeugen. Dieser Buffer wird dann gefüllt und kann von der 
            // jeweiligen Methode mit dem korrekten Typ zurückgegeben werden

            //var a = this.ReadCompactMultiDim<T[,,]>();
            var type = typeof(T);

            var lengths = new int[] { 100, 200, 10 };
            var size = lengths.Aggregate(1L, (x, y) => x * y);
            object[] args = lengths.Cast<object>().ToArray();

            var buffer = (T)Activator.CreateInstance(type, args);

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var span = new Span<byte>(handle.AddrOfPinnedObject().ToPointer(), (int)size);
                span.Fill(0x25);
                return buffer;
            }
            finally
            {
                handle.Free();
            }
        }

        private byte[] ReadCompact()
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

            return buffer;
        }

        private Stream ReadContiguousAsStream(H5DatasetAccess datasetAccess)
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

            if (this.Context.Superblock.IsUndefinedAddress(address))
            {
                if (this.ExternalFileList != null)
                    return new ExternalFileListStream(this.ExternalFileList, datasetAccess);

                else if (this.FillValue.IsDefined)
                    return new UnsafeFillValueStream(this.FillValue.Value);

                else
                    return new UnsafeFillValueStream(new byte[0]);
            }
            else
            {
                this.Context.Reader.Seek((long)address, SeekOrigin.Begin);
                return new OffsetStream(this.Context.Reader.BaseStream, this.Context.Reader.BaseStream.Position);
            }
        }

        private ulong[] GetChunkDims()
        {
            var chunkDims = default(ulong[]);

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                chunkDims = layout12
                    .DimensionSizes
                    .Select(value => (ulong)value)
                    .ToArray();
            }
            else if (this.DataLayout is DataLayoutMessage3 layout3 && !(this.DataLayout is DataLayoutMessage4))
            {
                var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;

                chunkDims = chunked3
                    .DimensionSizes[..^1]
                    .Select(value => (ulong)value)
                    .ToArray();
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;

                chunkDims = chunked4
                    .DimensionSizes[..^1]
                    .ToArray();
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            return chunkDims;
        }

        private byte[] ReadChunk(ulong[] chunkIndices)
        {
            byte[] buffer;

            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                var chunkByteSize = H5Utils.CalculateSize(layout12.DimensionSizes);
                buffer = new byte[chunkByteSize];

                if (this.Context.Superblock.IsUndefinedAddress(layout12.DataAddress))
                {
                    if (this.FillValue.IsDefined)
                        buffer.AsSpan().Fill(this.FillValue.Value);
                }
                else
                {
                    this.Context.Reader.Seek((int)layout12.DataAddress, SeekOrigin.Begin);
                    this.ReadBTree1Chunk(buffer, (byte)(layout12.Rank - 0), layout12.DimensionSizes, chunkIndices);
                }
            }
            else if (this.DataLayout is DataLayoutMessage3 layout3 && !(this.DataLayout is DataLayoutMessage4))
            {
                var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;
                var chunkByteSize = H5Utils.CalculateSize(chunked3.DimensionSizes);
                buffer = new byte[chunkByteSize];

                if (this.Context.Superblock.IsUndefinedAddress(chunked3.Address))
                {
                    if (this.FillValue.IsDefined)
                        buffer.AsSpan().Fill(this.FillValue.Value);
                }
                else
                {
                    this.Context.Reader.Seek((int)chunked3.Address, SeekOrigin.Begin);
                    this.ReadBTree1Chunk(buffer, (byte)(chunked3.Rank - 1), chunked3.DimensionSizes, chunkIndices);
                }
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;
                var chunkByteSize = H5Utils.CalculateSize(chunked4.DimensionSizes);
                buffer = new byte[chunkByteSize];

                if (this.Context.Superblock.IsUndefinedAddress(chunked4.Address))
                {
                    if (this.FillValue.IsDefined)
                        buffer.AsSpan().Fill(this.FillValue.Value);
                }
                else
                {
                    switch (chunked4.ChunkIndexingType)
                    {
                        // the current, maximum, and chunk dimension sizes are all the same
                        case ChunkIndexingType.SingleChunk:

                            if (chunked4.Flags.HasFlag(ChunkedStoragePropertyFlags.SINGLE_INDEX_WITH_FILTER))
                            {
                                var singleChunkInfo = (SingleChunkIndexingInformation)chunked4.IndexingTypeInformation;
                                this.SeekAndReadChunk(buffer, singleChunkInfo.FilteredChunkSize, singleChunkInfo.ChunkFilters, chunked4.Address);
                            }
                            else
                            {
                                this.SeekAndReadChunk(buffer, chunkByteSize, 0, chunked4.Address);
                            }

                            break;

                        // fixed maximum dimension sizes
                        // no filter applied to the dataset
                        // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                        case ChunkIndexingType.Implicit:

                            var implicitInfo = (ImplicitIndexingInformation)chunked4.IndexingTypeInformation;

                            var datasetDimsInChunkUnits1 = this.Dataspace.DimensionSizes
                                .Select((dim, i) => H5Utils.CeilDiv(dim, chunked4.DimensionSizes[i]))
                                .ToArray();

                            var chunkIndex1 = chunkIndices.ToLinearIndex(datasetDimsInChunkUnits1);
                            var chunkOffset = chunkIndex1 * chunkByteSize;
                            this.SeekAndReadChunk(buffer, chunkByteSize, 0, chunked4.Address + chunkOffset);
                            break;

                        // fixed maximum dimension sizes
                        case ChunkIndexingType.FixedArray:

                            var fixedArrayInfo = (FixedArrayIndexingInformation)chunked4.IndexingTypeInformation;

                            var datasetDimsInChunkUnits2 = this.Dataspace.DimensionSizes
                                .Select((dim, i) => H5Utils.CeilDiv(dim, chunked4.DimensionSizes[i]))
                                .ToArray();

                            var chunkIndex2 = chunkIndices.ToLinearIndex(datasetDimsInChunkUnits2);
                            this.Context.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);
                            this.ReadFixedArray(buffer, chunkByteSize, chunkIndex2);
                            break;

                        // only one dimension of unlimited extent
                        case ChunkIndexingType.ExtensibleArray:
                            var extensibleArrayInfo = (ExtensibleArrayIndexingInformation)chunked4.IndexingTypeInformation;
                            this.Context.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);
                            this.ReadExtensibleArray(buffer, chunkByteSize);
                            break;

                        // more than one dimension of unlimited extent
                        case ChunkIndexingType.BTree2:
                            var btree2Info = (BTree2IndexingInformation)chunked4.IndexingTypeInformation;
                            this.Context.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);
                            this.ReadBTree2Chunk(buffer, (byte)(chunked4.Rank - 1), chunkByteSize, chunkIndices);
                            break;

                        default:
                            break;
                    }
                }
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }

            return buffer;
        }

        // for later: H5EA__lookup_elmt

        private void ReadExtensibleArray(Memory<byte> buffer, ulong chunkSize)
        {
            //#error fill

            //            var chunkSizeLength = this.ComputeChunkSizeLength(chunkSize);
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
            //            }

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
        }

        private DataBlockElement[] ReadExtensibleArrayDataBlock(ExtensibleArrayHeader header, uint chunkSizeLength, uint elementIndex)
        {
            var secondaryBlockIndex = header.ComputeSecondaryBlockIndex(elementIndex + header.IndexBlockElementsCount);
            var elementsCount = header.SecondaryBlockInfos[secondaryBlockIndex].ElementsCount;
            var dataBlock = new ExtensibleArrayDataBlock(this.Context.Reader,
                                                         this.Context.Superblock,
                                                         header,
                                                         chunkSizeLength,
                                                         elementsCount);

            if (dataBlock.PageCount > 0)
            {
                var pages = new List<DataBlockPage>((int)dataBlock.PageCount);

                for (int i = 0; i < (int)dataBlock.PageCount; i++)
                {
                    var page = new DataBlockPage(this.Context.Reader,
                                                 this.Context.Superblock,
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

        private Memory<T> GetBuffer<T>(ulong totalCount, out T[] result)
            where T : unmanaged
        {
#warning review this when correcting code for generics

            // first, get byte size
            var byteSize = totalCount * this.Datatype.Size;

            // second, convert file type (e.g. 2 bytes) to T (e.g. 4 bytes)
            var arraySize = byteSize / (ulong)Unsafe.SizeOf<T>();

            // finally, create the buffer
            result = new T[arraySize];

            return result
                .AsMemory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SeekAndReadChunk(Memory<byte> buffer, ulong rawChunkSize, uint filterMask, ulong address)
        {
            this.Context.Reader.Seek((long)address, SeekOrigin.Begin);
            this.ReadChunk(buffer, rawChunkSize, filterMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadChunk(Memory<byte> buffer, ulong rawChunkSize, uint filterMask)
        {
            if (this.FilterPipeline == null)
            {
                this.Context.Reader.Read(buffer.Span);
            }
            else
            {
                using var filterBufferOwner = MemoryPool<byte>.Shared.Rent((int)rawChunkSize);
                var filterBuffer = filterBufferOwner.Memory[0..(int)rawChunkSize];
                this.Context.Reader.Read(filterBuffer.Span);

                H5Filter.ExecutePipeline(this.FilterPipeline.FilterDescriptions, filterMask, ExtendedFilterFlags.Reverse, filterBuffer, buffer);
            }
        }

        private void EnsureEndianness(Span<byte> buffer)
        {
            var byteOrderAware = this.Datatype.BitField as IByteOrderAware;

            if (byteOrderAware != null)
                H5Utils.EnsureEndianness(buffer.ToArray(), buffer, byteOrderAware.ByteOrder, this.Datatype.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int VectorCompare(byte rank, ulong[] v1, ulong[] v2)
        {
            for (int i = 0; i < rank; i++)
            {
                if (v1[i] < v2[i])
                    return -1;

                if (v1[i] > v2[i])
                    return 1;
            }

            return 0;
        }

        private IChunkCache GetChunkCache(H5DatasetAccess datasetAccess)
        {
            var chunkCacheFactory = datasetAccess.ChunkCacheFactory;

            if (chunkCacheFactory == null)
                chunkCacheFactory = _file.ChunkCacheFactory;

            if (chunkCacheFactory == null)
                chunkCacheFactory = H5File.DefaultChunkCacheFactory;

            return chunkCacheFactory();
        }

        #endregion
    }
}

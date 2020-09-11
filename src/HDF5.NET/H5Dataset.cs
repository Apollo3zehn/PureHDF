using System;
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
                var dimensionSizes = layout12.DimensionSizes.Select(value => (ulong)value).ToArray();
                var buffer = this.GetBuffer<T>(layout12.Dimensionality, dimensionSizes, layout12.DatasetElementSize, out var result);

                // read data
                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                this.File.Reader.Read(buffer);

                return result;
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var contiguous = (ContiguousStoragePropertyDescription)layout34.Properties;
                var buffer = this.GetBuffer<T>(1, new ulong[] { contiguous.Size }, 1, out var result);

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
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                return this.ReadChunkedBTree1<T>(layout12.DataAddress, layout12.Dimensionality, layout12.DimensionSizes, layout12.DatasetElementSize);
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;

                switch (chunked4.ChunkIndexingType)
                {
                    case ChunkIndexingType.SingleChunk:
                        // the current, maximum, and chunk dimension sizes are all the same
#warning untested
                        var singleChunk = (SingleChunkIndexingInformation)chunked4.IndexingTypeInformation;
                        var buffer = this.GetBuffer<T>(chunked4.Dimensionality, chunked4.DimensionSizes, this.DataType.Size, 1, out var result);

                        // read data
#warning if/else is filtered is missing
                        this.File.Reader.Seek((int)chunked4.Address, SeekOrigin.Begin);
                        this.File.Reader.Read(buffer);

                        break;

                    case ChunkIndexingType.Implicit:
                        // fixed maximum dimension sizes
                        // no filter applied to the dataset
                        // the timing for the space allocation of the dataset chunks is H5P_ALLOC_TIME_EARLY
                        var @implicit = (ImplicitIndexingInformation)chunked4.IndexingTypeInformation;
                        break;

                    case ChunkIndexingType.FixedArray:
                        var fixedArray = (FixedArrayIndexingInformation)chunked4.IndexingTypeInformation;
                        break;

                    case ChunkIndexingType.ExtensibleArray:
                        var extensibleArray = (ExtensibleArrayIndexingInformation)chunked4.IndexingTypeInformation;

                        break;
                    case ChunkIndexingType.BTree2:
                        throw new NotImplementedException();

                    default:
                        break;
                }

                //this.File.Reader.Seek((long)chunked4.Address, SeekOrigin.Begin);
                //var btree2 = new BTree2Header<BTree2Record10>(this.File.Reader, this.File.Superblock);
                throw new NotImplementedException();
            }
            else if (this.DataLayout is DataLayoutMessage3 layout3)
            {
                var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;
                return this.ReadChunkedBTree1<T>(chunked3.Address, chunked3.Dimensionality, chunked3.DimensionSizes, chunked3.DatasetElementSize);
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        private Span<byte> GetBuffer<T>(byte dimensionality, ulong[] dimensionSizes, ulong elementSize, out T[] result) where T : unmanaged
        {
            return this.GetBuffer(dimensionality, dimensionSizes, elementSize, 1, out result);
        }

        private Span<byte> GetBuffer<T>(byte dimensionality, ulong[] dimensionSizes, ulong elementSize, ulong repetitions, out T[] result) where T : unmanaged
        {
            var byteSize = this.CalculateByteSize(dimensionality, dimensionSizes, elementSize, repetitions);
            var arraySize = byteSize / (ulong)Unsafe.SizeOf<T>();
            result = new T[arraySize];
            var buffer = MemoryMarshal.AsBytes(result.AsSpan());

            return buffer;
        }

        private T[] ReadChunkedBTree1<T>(ulong address, byte dimensionality, uint[] dimensionSizes, uint dataElementSize)
            where T : unmanaged
        {
            // btree1
#warning may be the undefined address to indicate that storge is not yet allocated.
            this.File.Reader.Seek((int)address, SeekOrigin.Begin);
            var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock);
            var leafKeys = btree1.GetTree()[0];
            var childAddress = leafKeys.SelectMany(key => key.ChildAddresses).ToArray();
            var keys = leafKeys.SelectMany(key => key.Keys).ToArray();

            // buffer
            var chunkCount = (ulong)childAddress.Length;
            dimensionality -= 1;
            var dimensionSizes_ulong = dimensionSizes
                .Select(value => (ulong)value)
                .ToArray();

            var buffer = this.GetBuffer<T>(dimensionality, dimensionSizes_ulong, dataElementSize, chunkCount, out var result);

            // read data
            var offset = 0;

            for (ulong i = 0; i < chunkCount; i++)
            {
#warning if/else is filtered is missing
                var chunkSize = (int)keys[i].ChunkSize;
                this.File.Reader.Seek((long)childAddress[i], SeekOrigin.Begin);
                this.File.Reader.Read(buffer.Slice(offset, chunkSize));
                offset += chunkSize;
            }

            return result;
        }

        private ulong CalculateByteSize(byte dimensionality, ulong[] dimensionSizes, ulong elementSize, ulong repetitions)
        {
            var byteSize = 0UL;

            if (dimensionality > 0)
            {
                byteSize = dimensionSizes.Aggregate((x, y) => x * y);
                byteSize *= elementSize;
                byteSize *= repetitions;
            }

            return byteSize;
        }

        #endregion
    }
}

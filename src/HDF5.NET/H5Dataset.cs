using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public Span<T> Read<T>() where T : unmanaged
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

        private Span<T> ReadCompact<T>() where T : unmanaged
        {
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                return MemoryMarshal.Cast<byte, T>(layout12.CompactData);
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                return MemoryMarshal.Cast<byte, T>(compact.RawData);
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        private Span<T> ReadContiguous<T>() where T : unmanaged
        {
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                var size = 0UL;

                if (layout12.Dimensionality > 0)
                {
                    size = this.Dataspace.DimensionSizes.Aggregate((x, y) => x * y);
                    size *= layout12.DatasetElementSize;
                }

                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                var data = this.File.Reader.ReadBytes((int)size);
                return MemoryMarshal.Cast<byte, T>(data);
            }
            else if (this.DataLayout is DataLayoutMessage3 layout34)
            {
                var contiguous = (ContiguousStoragePropertyDescription)layout34.Properties;
                var size = contiguous.Size;

                this.File.Reader.Seek((long)contiguous.Address, SeekOrigin.Begin);
                var data = this.File.Reader.ReadBytes((int)size);
                return MemoryMarshal.Cast<byte, T>(data);
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        private Span<T> ReadChunked<T>() where T : unmanaged
        {
            if (this.DataLayout is DataLayoutMessage12 layout12)
            {
                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock);
                throw new NotImplementedException();
            }
            else if (this.DataLayout is DataLayoutMessage4 layout4)
            {
                var chunked4 = (ChunkedStoragePropertyDescription4)layout4.Properties;

                switch (chunked4.ChunkIndexingType)
                {
                    case ChunkIndexingType.SingleChunk:
                        var singleChunk = (SingleChunkIndexingInformation)chunked4.IndexingTypeInformation;
                        break;

                    case ChunkIndexingType.Implicit:
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

                this.File.Reader.Seek((long)chunked3.Address, SeekOrigin.Begin);
                var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock);
                var a = btree1.GetTree();
                throw new NotImplementedException();
            }
            else
            {
                throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
            }
        }

        #endregion
    }
}

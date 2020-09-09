using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

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
                var type = message.GetType();

                if (type == typeof(DataLayoutMessage))
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
                case LayoutClass.Compact:
                    return this.ReadCompact<T>();

                case LayoutClass.Contiguous:
                    return this.ReadContiguous<T>();

                case LayoutClass.Chunked:
                    return this.ReadChunked<T>();

                case LayoutClass.VirtualStorage:
                    throw new NotImplementedException();

                default:
                    throw new Exception($"The data layout class '{this.DataLayout.LayoutClass}' is not supported.");
            }
        }

        private Span<T> ReadCompact<T>() where T : unmanaged
        {
            var layout12 = this.DataLayout as DataLayoutMessage12;

            if (layout12 != null)
            {
                return MemoryMarshal.Cast<byte, T>(layout12.CompactData);
            }
            else
            {
                var layout34 = this.DataLayout as DataLayoutMessage3;

                if (layout34 != null)
                {
                    var compact = (CompactStoragePropertyDescription)layout34.Properties;
                    return MemoryMarshal.Cast<byte, T>(compact.RawData);
                }
                else
                {
                    throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
                }
            }
        }

        private Span<T> ReadContiguous<T>() where T : unmanaged
        {
            var layout12 = this.DataLayout as DataLayoutMessage12;

            if (layout12 != null)
            {
#warning Can dimensionality be zero?
#warning Does dataspace have same dims?
                var size = layout12.DimensionSizes.Aggregate((x, y) => x * y);
                size *= layout12.DatasetElementSize;

                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                var data = this.File.Reader.ReadBytes((int)size);
                return MemoryMarshal.Cast<byte, T>(data);
            }
            else
            {
                var layout34 = this.DataLayout as DataLayoutMessage3;

                if (layout34 != null)
                {
                    var compact = (ContiguousStoragePropertyDescription)layout34.Properties;
                    var size = compact.Size;

                    this.File.Reader.Seek((long)compact.Address, SeekOrigin.Begin);
                    var data = this.File.Reader.ReadBytes((int)size);
                    return MemoryMarshal.Cast<byte, T>(data);
                }
                else
                {
                    throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
                }
            }
        }

        private Span<T> ReadChunked<T>() where T : unmanaged
        {
            var layout12 = this.DataLayout as DataLayoutMessage12;

            if (layout12 != null)
            {
                this.File.Reader.Seek((long)layout12.DataAddress, SeekOrigin.Begin);
                var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock);
                throw new NotImplementedException();
            }
            else
            {
                var layout3 = this.DataLayout as DataLayoutMessage3;

                if (layout3 != null)
                {
                    var chunked3 = (ChunkedStoragePropertyDescription3)layout3.Properties;
                    
                    this.File.Reader.Seek((long)chunked3.Address, SeekOrigin.Begin);
                    var btree1 = new BTree1Node<BTree1RawDataChunksKey>(this.File.Reader, this.File.Superblock);
                    throw new NotImplementedException();
                }
                else
                {
                    var layout4 = this.DataLayout as DataLayoutMessage4;

                    if (layout4 != null)
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
                    else
                    {
                        throw new Exception($"Data layout message type '{this.DataLayout.GetType().Name}' is not supported.");
                    }
                }
            }
        }

        #endregion
    }
}

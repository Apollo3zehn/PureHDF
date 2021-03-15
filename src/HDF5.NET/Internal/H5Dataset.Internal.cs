using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HDF5.NET
{
    public partial class H5Dataset : H5AttributableObject
    {
        #region Constructors

        internal H5Dataset(H5File file, H5Context context, H5NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            this.File = file;

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
            if (this.DataLayout is null)
                throw new Exception("The data layout message is missing.");

            if (this.Dataspace is null)
                throw new Exception("The dataspace message is missing.");

            if (this.Datatype is null)
                throw new Exception("The data type message is missing.");

            if (this.FillValue is null)
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
            // short path for null dataspace
            if (this.Dataspace.Type == DataspaceType.Null)
                return new T[0];

            // 
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
            if (skipShuffle && this.FilterPipeline is not null)
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

            /* buffer provider */
            H5D_Base bufferProvider = this.DataLayout.LayoutClass switch
            {
                /* Compact: The array is stored in one contiguous block as part of
                 * this object header message. 
                 */
                LayoutClass.Compact => new H5D_Compact(this, datasetAccess),

                /* Contiguous: The array is stored in one contiguous area of the file. 
                * This layout requires that the size of the array be constant: 
                * data manipulations such as chunking, compression, checksums, 
                * or encryption are not permitted. The message stores the total
                * storage size of the array. The offset of an element from the 
                * beginning of the storage area is computed as in a C array.
                */
                LayoutClass.Contiguous => new H5D_Contiguous(this, datasetAccess),

                /* Chunked: The array domain is regularly decomposed into chunks,
                 * and each chunk is allocated and stored separately. This layout 
                 * supports arbitrary element traversals, compression, encryption,
                 * and checksums (these features are described in other messages).
                 * The message stores the size of a chunk instead of the size of the
                 * entire array; the storage size of the entire array can be 
                 * calculated by traversing the chunk index that stores the chunk 
                 * addresses. 
                 */
                LayoutClass.Chunked => H5D_Chunk.Create(this, datasetAccess),

                /* Virtual: This is only supported for version 4 of the Data Layout 
                 * message. The message stores information that is used to locate 
                 * the global heap collection containing the Virtual Dataset (VDS) 
                 * mapping information. The mapping associates the VDS to the source
                 * dataset elements that are stored across a collection of HDF5 files.
                 */
                LayoutClass.VirtualStorage => throw new NotImplementedException(),

                /* default */
                _ => throw new Exception($"The data layout class '{this.DataLayout.LayoutClass}' is not supported.")
            };

            bufferProvider.Initialize();

            Func<ulong[], Memory<byte>>? getSourceBuffer = bufferProvider.SupportsBuffer
               ? chunkIndices => bufferProvider.GetBuffer(chunkIndices)
               : null;

            Func<ulong[], Stream>? getSourceStream = bufferProvider.SupportsStream
                ? chunkIndices => bufferProvider.GetStream(chunkIndices)
                : null;

            /* dataset dims */
            var datasetDims = bufferProvider.GetDatasetDims();

            /* dataset chunk dims */
            var datasetChunkDims = bufferProvider.GetChunkDims();

            /* file selection */
            if (fileSelection is null)
                fileSelection = bufferProvider.GetSelection();

            /* result buffer */
            var result = default(T[]);
            var totalCount = fileSelection.GetTotalCount();

            if (buffer.Equals(default))
                buffer = this.GetBuffer(totalCount, out result);

            /* memory selection */
            if (memorySelection is null)
                memorySelection = new HyperslabSelection(start: 0, block: totalCount);

            /* check both selections */
            var fileHyperslabSelection = fileSelection as HyperslabSelection;
            var memoryHyperslabSelection = memorySelection as HyperslabSelection;

            if (fileHyperslabSelection is null || memoryHyperslabSelection is null)
                throw new NotSupportedException("Only hyperslab selections are currently supported.");

            /* memory dims */
            if (memoryDims is null)
                memoryDims = new ulong[] { totalCount };

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
                bufferProvider.Dispose();
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

        private void EnsureEndianness(Span<byte> buffer)
        {
            var byteOrderAware = this.Datatype.BitField as IByteOrderAware;

            if (byteOrderAware is not null)
                H5Utils.EnsureEndianness(buffer.ToArray(), buffer, byteOrderAware.ByteOrder, this.Datatype.Size);
        }

        #endregion
    }
}

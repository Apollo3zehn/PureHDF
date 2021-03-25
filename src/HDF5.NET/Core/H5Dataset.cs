using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Datatype.Class}'")]
    partial class H5Dataset
    {
        #region Fields

        private H5Dataspace _space;
        private H5DataType _type;
        private H5DataLayout _layout;
        private H5FillValue _fillValue;

        #endregion

        #region Constructors

        internal H5Dataset(H5File file, H5Context context, NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            this.File = file;

            foreach (var message in this.Header.HeaderMessages)
            {
                var type = message.Data.GetType();

                if (typeof(DataLayoutMessage).IsAssignableFrom(type))
                    this.InternalDataLayout = (DataLayoutMessage)message.Data;

                else if (type == typeof(DataspaceMessage))
                    this.InternalDataspace = (DataspaceMessage)message.Data;

                else if (type == typeof(DatatypeMessage))
                    this.InternalDataType = (DatatypeMessage)message.Data;

                else if (type == typeof(FillValueMessage))
                    this.InternalFillValue = (FillValueMessage)message.Data;

                else if (type == typeof(FilterPipelineMessage))
                    this.InternalFilterPipeline = (FilterPipelineMessage)message.Data;

                else if (type == typeof(ObjectModificationMessage))
                    this.InternalObjectModification = (ObjectModificationMessage)message.Data;

                else if (type == typeof(ExternalFileListMessage))
                    this.InternalExternalFileList = (ExternalFileListMessage)message.Data;
            }

            // check that required fields are set
            if (this.InternalDataLayout is null)
                throw new Exception("The data layout message is missing.");

            if (this.InternalDataspace is null)
                throw new Exception("The dataspace message is missing.");

            if (this.InternalDataType is null)
                throw new Exception("The data type message is missing.");

            if (this.InternalFillValue is null)
                throw new Exception("The fill value message is missing.");
        }

        #endregion

        #region Properties

        internal DataLayoutMessage InternalDataLayout { get; } = null!;

        internal DataspaceMessage InternalDataspace { get; } = null!;

        internal DatatypeMessage InternalDataType { get; } = null!;

        internal FillValueMessage InternalFillValue { get; } = null!;

        internal FilterPipelineMessage? InternalFilterPipeline { get; }

        internal ObjectModificationMessage? InternalObjectModification { get; }

        internal ExternalFileListMessage? InternalExternalFileList { get; }

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
            if (this.InternalDataspace.Type == DataspaceType.Null)
                return new T[0];

            // 
            if (!skipTypeCheck)
            {
                switch (this.InternalDataType.Class)
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
            if (skipShuffle && this.InternalFilterPipeline is not null)
            {
                var filtersToRemove = this
                    .InternalFilterPipeline
                    .FilterDescriptions
                    .Where(description => description.Identifier == FilterIdentifier.Shuffle)
                    .ToList();

                foreach (var filter in filtersToRemove)
                {
                    this.InternalFilterPipeline.FilterDescriptions.Remove(filter);
                }
            }

            /* buffer provider */
            using H5D_Base bufferProvider = this.InternalDataLayout.LayoutClass switch
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
                _ => throw new Exception($"The data layout class '{this.InternalDataLayout.LayoutClass}' is not supported.")
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
            var byteSize = totalCount * this.InternalDataType.Size;

            if (buffer.Equals(default))
                buffer = this.GetBuffer(byteSize, out result);

            else if ((ulong)MemoryMarshal.AsBytes(buffer.Span).Length < byteSize)
                throw new Exception("The provided target buffer is too small.");

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

            if (getSourceBuffer is null && getSourceStream is null)
                new Exception($"The data layout class '{this.InternalDataLayout.LayoutClass}' is not supported.");

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
                TypeSize: (int)this.InternalDataType.Size
            );

            HyperslabUtils.Copy(fileHyperslabSelection.Rank, memoryHyperslabSelection.Rank, copyInfo);

            /* ensure correct endianness */
            var byteOrderAware = this.InternalDataType.BitField as IByteOrderAware;
            var destination = MemoryMarshal.AsBytes(buffer.Span);
            var source = destination.ToArray();

            if (byteOrderAware is not null)
                H5Utils.EnsureEndianness(source, destination, byteOrderAware.ByteOrder, this.InternalDataType.Size);

            /* return */
            return result;
        }

        private Memory<T> GetBuffer<T>(ulong byteSize, out T[] result)
            where T : unmanaged
        {
            // convert file type (e.g. 2 bytes) to T (e.g. custom struct with 35 bytes)
            var sizeOfT = (ulong)Unsafe.SizeOf<T>();

            if (byteSize % sizeOfT != 0)
                throw new Exception("The size of the target buffer (number of selected elements times the datasets data-type byte size) must be a multiple of the byte size of the generic parameter T.");

            var arraySize = byteSize / (ulong)Unsafe.SizeOf<T>();

            // create the buffer
            result = new T[arraySize];

            return result
                .AsMemory();
        }

        #endregion
    }
}

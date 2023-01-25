using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PureHDF
{
    [DebuggerDisplay("{Name}: Class = '{InternalDataType.Class}'")]
    partial class H5Dataset : H5AttributableObject
    {
        #region Fields

        private H5Dataspace? _space;
        private H5DataType? _type;
        private H5DataLayout? _layout;
        private H5FillValue? _fillValue;

        #endregion

        #region Constructors

        internal H5Dataset(H5File file, H5Context context, NamedReference reference, ObjectHeader header)
            : base(context, reference, header)
        {
            File = file;

            foreach (var message in Header.HeaderMessages)
            {
                var type = message.Data.GetType();

                if (typeof(DataLayoutMessage).IsAssignableFrom(type))
                    InternalDataLayout = (DataLayoutMessage)message.Data;

                else if (type == typeof(DataspaceMessage))
                    InternalDataspace = (DataspaceMessage)message.Data;

                else if (type == typeof(DatatypeMessage))
                    InternalDataType = (DatatypeMessage)message.Data;

                else if (type == typeof(FillValueMessage))
                    InternalFillValue = (FillValueMessage)message.Data;

                else if (type == typeof(FilterPipelineMessage))
                    InternalFilterPipeline = (FilterPipelineMessage)message.Data;

                else if (type == typeof(ObjectModificationMessage))
                    InternalObjectModification = (ObjectModificationMessage)message.Data;

                else if (type == typeof(ExternalFileListMessage))
                    InternalExternalFileList = (ExternalFileListMessage)message.Data;
            }

            // check that required fields are set
            if (InternalDataLayout is null)
                throw new Exception("The data layout message is missing.");

            if (InternalDataspace is null)
                throw new Exception("The dataspace message is missing.");

            if (InternalDataType is null)
                throw new Exception("The data type message is missing.");

            // https://github.com/Apollo3zehn/PureHDF/issues/25
            if (InternalFillValue is null)
            {
                // The OldFillValueMessage is optional and so there might be not fill value
                // message at all although the newer message is being marked as required. The
                // workaround is to instantiate a new FillValueMessage with sensible defaults.
                // It is not 100% clear if these defaults are fine.

                var allocationTime = InternalDataLayout.LayoutClass == LayoutClass.Chunked
                    ? SpaceAllocationTime.Incremental
                    : SpaceAllocationTime.Late;

                InternalFillValue = new FillValueMessage(allocationTime);
            }
        }

        #endregion

        #region Properties

        internal DataLayoutMessage InternalDataLayout { get; } = default!;

        internal DataspaceMessage InternalDataspace { get; } = default!;

        internal DatatypeMessage InternalDataType { get; } = default!;

        internal FillValueMessage InternalFillValue { get; } = default!;

        internal FilterPipelineMessage? InternalFilterPipeline { get; }

        internal ObjectModificationMessage? InternalObjectModification { get; }

        internal ExternalFileListMessage? InternalExternalFileList { get; }

        #endregion

        #region Private

        internal async Task<T[]?> ReadAsync<T, TReader>(
            TReader reader,
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default,
            bool skipTypeCheck = false,
            bool skipShuffle = false)
                where T : unmanaged
                where TReader : IReader
        {
            // fast path for null dataspace
            if (InternalDataspace.Type == DataspaceType.Null)
                return Array.Empty<T>();

            // 
            if (!skipTypeCheck)
            {
                switch (InternalDataType.Class)
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
            if (skipShuffle && InternalFilterPipeline is not null)
            {
                var filtersToRemove = this
                    .InternalFilterPipeline
                    .FilterDescriptions
                    .Where(description => description.Identifier == FilterIdentifier.Shuffle)
                    .ToList();

                foreach (var filter in filtersToRemove)
                {
                    InternalFilterPipeline.FilterDescriptions.Remove(filter);
                }
            }

            /* buffer provider */
            using H5D_Base bufferProvider = InternalDataLayout.LayoutClass switch
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
                LayoutClass.VirtualStorage => new H5D_Virtual(this, datasetAccess),

                /* default */
                _ => throw new Exception($"The data layout class '{InternalDataLayout.LayoutClass}' is not supported.")
            };

            bufferProvider.Initialize();

            Func<ulong[], Task<Memory<byte>>>? getSourceBufferAsync = bufferProvider.SupportsBuffer
               ? chunkIndices => bufferProvider.GetBufferAsync(reader, chunkIndices)
               : null;

            Func<ulong[], Stream>? getSourceStream = bufferProvider.SupportsStream
                ? chunkIndices => bufferProvider.GetH5Stream(chunkIndices)!
                : null;

            /* dataset dims */
            var datasetDims = GetDatasetDims();

            /* dataset chunk dims */
            var datasetChunkDims = bufferProvider.GetChunkDims();

            /* file selection */
            if (fileSelection is null)
            {
                switch (InternalDataspace.Type)
                {
                    case DataspaceType.Scalar:
                    case DataspaceType.Simple:

                        var starts = datasetDims.ToArray();
                        starts.AsSpan().Fill(0);

                        fileSelection = new HyperslabSelection(rank: datasetDims.Length, starts: starts, blocks: datasetDims);

                        break;

                    case DataspaceType.Null:
                    default:
                        throw new Exception($"Unsupported data space type '{InternalDataspace.Type}'.");
                }
            }

            /* result buffer */
            var result = default(T[]);
            var totalCount = fileSelection.TotalElementCount;
            var byteSize = totalCount * InternalDataType.Size;

            Memory<byte> byteBuffer;

            // user did not provide buffer
            if (buffer.Equals(default))
                (result, byteBuffer) = GetBuffer<T>(byteSize);

            // user provided buffer is large enough
            else if ((ulong)MemoryMarshal.AsBytes(buffer.Span).Length >= byteSize)
                byteBuffer = buffer.Cast<T, byte>();

            // user provided buffer is too small
            else
                throw new Exception("The provided target buffer is too small.");

            /* memory selection */
            memorySelection ??= new HyperslabSelection(start: 0, block: totalCount);

            /* memory dims */
            memoryDims ??= new ulong[] { totalCount };

            if (getSourceBufferAsync is null && getSourceStream is null)
                throw new Exception($"The data layout class '{InternalDataLayout.LayoutClass}' is not supported.");

            /* copy info */
            var copyInfo = new CopyInfo(
                datasetDims,
                datasetChunkDims,
                memoryDims,
                memoryDims,
                fileSelection,
                memorySelection,
                GetSourceBufferAsync: getSourceBufferAsync,
                GetSourceStream: getSourceStream,
                GetTargetBuffer: indices => byteBuffer,
                TypeSize: (int)InternalDataType.Size
            );

            await SelectionUtils.CopyAsync<TReader>(reader, datasetChunkDims.Length, memoryDims.Length, copyInfo).ConfigureAwait(false);

            /* ensure correct endianness */
            var byteOrderAware = InternalDataType.BitField as IByteOrderAware;
            var source = byteBuffer.Span.ToArray();

            if (byteOrderAware is not null)
                H5Utils.EnsureEndianness(source, byteBuffer.Span, byteOrderAware.ByteOrder, InternalDataType.Size);

            /* return */
            return result;
        }

        private static (T[], Memory<byte>) GetBuffer<T>(ulong byteSize)
            where T : unmanaged
        {
            // convert file type (e.g. 2 bytes) to T (e.g. custom struct with 35 bytes)
            var sizeOfT = (ulong)Unsafe.SizeOf<T>();

            if (byteSize % sizeOfT != 0)
                throw new Exception("The size of the target buffer (number of selected elements times the datasets data-type byte size) must be a multiple of the byte size of the generic parameter T.");

            var arraySize = byteSize / sizeOfT;

            // create the buffer
            var TBuffer = new T[arraySize];

            var byteBuffer = TBuffer
                .AsMemory()
                .Cast<T, byte>();

            return (TBuffer, byteBuffer);
        }

        internal ulong[] GetDatasetDims()
        {
            return InternalDataspace.Type switch
            {
                DataspaceType.Scalar => new ulong[] { 1 },
                DataspaceType.Simple => InternalDataspace.DimensionSizes,
                _ => throw new Exception($"Unsupported data space type '{InternalDataspace.Type}'.")
            };
        }

        #endregion
    }
}

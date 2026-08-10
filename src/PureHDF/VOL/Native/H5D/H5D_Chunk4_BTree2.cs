using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class H5D_Chunk4_BTree2 : H5D_Chunk4
{
    #region Fields

    private BTree2Header<BTree2Record10>? _btree2_no_filter;

    private BTree2Header<BTree2Record11>? _btree2_filter;

    #endregion

    #region Constructors
    public H5D_Chunk4_BTree2(
        NativeReadContext readContext,
        NativeWriteContext writeContext,
        DatasetInfo dataset,
        DataLayoutMessage4 layout,
        H5DatasetAccess datasetAccess,
        H5DatasetCreation datasetCreation)
        : base(readContext, writeContext, dataset, layout, datasetAccess, datasetCreation)
    {
        //
    }

    #endregion

    #region Methods

    protected override async ValueTask<ChunkInfo> GetReadChunkInfo(ulong chunkIndex)
    {
        var chunkIndices = MathUtils.ToCoordinates(chunkIndex, ScaledDims);

        if (Dataset.FilterPipeline is null)
        {
            if (_btree2_no_filter is null)
            {
                // Cached per file and per address, not per H5D_Base: NativeDataset builds a fresh
                // H5D_Base for every Read (NativeDataset.cs), so this field used to die with the call
                // and every read of a chunked dataset re-decoded the whole chunk index from scratch.
                _btree2_no_filter = await NativeCache.GetStructure(
                    ReadContext,
                    Chunked4.Address,
                    (DecodeKeyDelegate<BTree2Record10>)DecodeRecord10,
                    static (c, dk) => BTree2Header<BTree2Record10>.Decode(c, dk)).ConfigureAwait(false);
            }

            // get record
            var (success, record) = await _btree2_no_filter.TryFindRecord(ReadContext, DecodeRecord10, record =>
            {
                // H5Dbtree2.c (H5D__bt2_compare)
                return new ValueTask<int>(MathUtils.VectorCompare(ChunkRank, chunkIndices, record.ScaledOffsets));
            }).ConfigureAwait(false);

            return success
                ? new ChunkInfo(record.Address, ChunkByteSize, 0)
                : ChunkInfo.None;
        }
        else
        {
            if (_btree2_filter is null)
            {
                // Cached per file and per address, not per H5D_Base: NativeDataset builds a fresh
                // H5D_Base for every Read (NativeDataset.cs), so this field used to die with the call
                // and every read of a chunked dataset re-decoded the whole chunk index from scratch.
                _btree2_filter = await NativeCache.GetStructure(
                    ReadContext,
                    Chunked4.Address,
                    (DecodeKeyDelegate<BTree2Record11>)DecodeRecord11,
                    static (c, dk) => BTree2Header<BTree2Record11>.Decode(c, dk)).ConfigureAwait(false);
            }

            // get record
            var (success, record) = await _btree2_filter.TryFindRecord(ReadContext, DecodeRecord11, record =>
            {
                // H5Dbtree2.c (H5D__bt2_compare)
                return new ValueTask<int>(MathUtils.VectorCompare(ChunkRank, chunkIndices, record.ScaledOffsets));
            }).ConfigureAwait(false);

            return success
                ? new ChunkInfo(record.Address, record.ChunkSize, record.FilterMask)
                : ChunkInfo.None;
        }
    }

    protected override ChunkInfo GetActualWriteChunkInfo(ulong chunkIndex, uint chunkSize, uint filterMask)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Callbacks

    // Both match DecodeKeyDelegate<T>: the rank and the chunk-size length are per-dataset constants
    // derived from this instance, so only the context has to be threaded through - and it must be the
    // CALLER's, not ReadContext, so that a record decode uses the same driver as the traversal.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<BTree2Record10> DecodeRecord10(NativeReadContext context)
    {
        return BTree2Record10.Decode(context, ChunkRank);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<BTree2Record11> DecodeRecord11(NativeReadContext context)
    {
        // ChunkSizeLength, not a fresh MathUtils.ComputeChunkSizeLength(ChunkByteSize): the base class
        // already computes exactly that once during initialization, and this runs per record decode.
        return BTree2Record11.Decode(context, ChunkRank, ChunkSizeLength);
    }

    #endregion
}
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
                ReadContext.Driver.SeekRelativeToBaseAddress((long)Chunked4.Address);

                ValueTask<BTree2Record10> decodeKey() => DecodeRecord10(ChunkRank);

                _btree2_no_filter = await BTree2Header<BTree2Record10>.Decode(ReadContext, decodeKey).ConfigureAwait(false);
            }

            // get record
            var (success, record) = await _btree2_no_filter.TryFindRecord(record =>
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
                ReadContext.Driver.SeekRelativeToBaseAddress((long)Chunked4.Address);
                var chunkSizeLength = MathUtils.ComputeChunkSizeLength(ChunkByteSize);

                ValueTask<BTree2Record11> decodeKey() => DecodeRecord11(ChunkRank, chunkSizeLength);

                _btree2_filter = await BTree2Header<BTree2Record11>.Decode(ReadContext, decodeKey).ConfigureAwait(false);
            }

            // get record
            var (success, record) = await _btree2_filter.TryFindRecord(record =>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<BTree2Record10> DecodeRecord10(byte rank)
    {
        return BTree2Record10.Decode(ReadContext, rank);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ValueTask<BTree2Record11> DecodeRecord11(byte rank, uint chunkSizeLength)
    {
        return BTree2Record11.Decode(ReadContext, rank, chunkSizeLength);
    }

    #endregion
}
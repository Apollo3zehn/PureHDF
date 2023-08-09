﻿namespace PureHDF;

internal class H5D_Chunk4_Implicit : H5D_Chunk4
{
    public H5D_Chunk4_Implicit(NativeReadContext readContext, NativeWriteContext writeContext, DatasetInfo dataset, DataLayoutMessage4 layout, H5DatasetAccess datasetAccess) :
        base(readContext, writeContext, dataset, layout, datasetAccess)
    {
        //
    }

    protected override ChunkInfo GetReadChunkInfo(ulong[] chunkIndices)
    {
        var chunkIndex = chunkIndices.ToLinearIndexPrecomputed(DownMaxChunkCounts);
        var chunkOffset = chunkIndex * ChunkByteSize;

        return new ChunkInfo(Dataset.Layout.Address + chunkOffset, ChunkByteSize, 0);
    }

    protected override ChunkInfo GetWriteChunkInfo(ulong[] chunkIndices, uint chunkSize, uint filterMask)
    {
        return GetReadChunkInfo(chunkIndices);
    }
}
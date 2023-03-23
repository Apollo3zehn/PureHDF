﻿namespace PureHDF.VOL.Native;

internal class FilteredDataBlockElement : DataBlockElement
{
    #region Properties

    public uint ChunkSize { get; set; }

    public uint FilterMask { get; set; }

    #endregion
}
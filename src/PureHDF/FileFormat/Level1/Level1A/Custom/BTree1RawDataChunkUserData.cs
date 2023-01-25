namespace PureHDF
{
    internal struct BTree1RawDataChunkUserData
    {
        #region Properties

        public ulong ChunkSize { get; set; }

        public ulong ChildAddress { get; set; }

        public uint FilterMask { get; set; }

        #endregion
    }
}

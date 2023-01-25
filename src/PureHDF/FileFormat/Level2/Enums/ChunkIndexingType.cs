namespace PureHDF
{
    internal enum ChunkIndexingType : byte
    {
        SingleChunk = 1,
        Implicit = 2,
        FixedArray = 3,
        ExtensibleArray = 4,
        BTree2 = 5
    }
}

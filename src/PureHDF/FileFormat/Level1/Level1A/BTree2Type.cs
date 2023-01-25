namespace PureHDF
{
    internal enum BTree2Type : byte
    {
        Testing = 0,
        IndexingIndirectlyAccessed_NonFilteredHugeFractalHeapObjects = 1,
        IndexingIndirectlyAccessed_FilteredHugeFractalHeapObjects = 2,
        IndexingDirectlyAccessed_NonFilteredHugeFractalHeapObjects = 3,
        IndexingDirectlyAccessed_FilteredHugeFractalHeapObjects = 4,
        IndexingNameField_Links = 5,
        IndexingCreationOrderField_Links = 6,
        IndexingSharedObjectHeaderMessages = 7,
        IndexingNameField_Attributes = 8,
        IndexingCreationOrderField_Attributes = 9,
        IndexingChunksOfDatasets_WithoutFilters_WithMoreThanOneUnlimDim = 10,
        IndexingChunksOfDatasets_WithFilters_WithMoreThanOneUnlimDim = 11,
    }
}

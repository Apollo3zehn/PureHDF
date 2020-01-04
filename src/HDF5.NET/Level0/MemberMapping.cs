namespace HDF5.NET
{
    public enum MemberMapping : byte
    {
        Superblock = 1,
        BTree = 2,
        Raw = 3,
        GlobalHeap = 4,
        LocalHeap = 5,
        ObjectHeader = 6
    }
}

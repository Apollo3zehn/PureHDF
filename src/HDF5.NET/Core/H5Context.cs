namespace HDF5.NET
{
    internal record struct H5Context(H5BinaryReader Reader, Superblock Superblock);
}

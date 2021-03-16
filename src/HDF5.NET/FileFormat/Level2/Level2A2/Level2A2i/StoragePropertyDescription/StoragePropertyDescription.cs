namespace HDF5.NET
{
    internal abstract class StoragePropertyDescription : FileBlock
    {
        public StoragePropertyDescription(H5BinaryReader reader) : base(reader)
        {
            //
        }

        public ulong Address { get; protected set; } = Superblock.UndefinedAddress;
    }
}

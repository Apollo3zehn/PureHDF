namespace PureHDF.VOL.Native;

internal abstract class StoragePropertyDescription
{
    public StoragePropertyDescription()
    {
        //
    }

    public ulong Address { get; protected set; } = Superblock.UndefinedAddress;
}
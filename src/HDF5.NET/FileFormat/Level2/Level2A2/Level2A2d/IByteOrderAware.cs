namespace HDF5.NET
{
    internal interface IByteOrderAware
    {
        ByteOrder ByteOrder { get; set; }
    }
}
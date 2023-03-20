using System.Reflection;

namespace PureHDF.VOL.Hsds;

internal class HsdsAttribute : IH5Attribute
{
    public HsdsAttribute(string name, HsdsDataspace space, HsdsDataType type)
    {
        Name = name;
        Space = space;
        Type = type;
    }

    public string Name { get; }

    public IH5Dataspace Space { get; }

    public IH5DataType Type { get; }

    public T[] Read<T>() where T : unmanaged
    {
        throw new NotImplementedException();
    }

    public T[] ReadCompound<T>(Func<FieldInfo, string>? getName = null) where T : struct
    {
        throw new NotImplementedException();
    }

    public Dictionary<string, object?>[] ReadCompound()
    {
        throw new NotImplementedException();
    }

    public string[] ReadString()
    {
        throw new NotImplementedException();
    }
}
using System.Reflection;
using System.Text.Json;
using Hsds.Api;
using PureHDF.Selections;

namespace PureHDF.VOL.Hsds;

internal class HsdsAttribute : IH5Attribute
{
    private readonly AttributeType _attribute;
    private readonly HsdsClient _client;
    private IH5Dataspace? _space;
    private IH5DataType? _type;

    public HsdsAttribute(AttributeType attribute, HsdsClient client)
    {
        _attribute = attribute;
        Name = attribute.Name;
        _client = client;
    }

    public string Name { get; }

    public IH5Dataspace Space
    {
        get
        {
            _space ??= new HsdsDataspace(_attribute.Shape);
            return _space;
        }
    }

    public IH5DataType Type
    {
        get
        {
            _type ??= new HsdsDataType(_attribute.Type);
            return _type;
        }
    }

    public T[] Read<T>() where T : unmanaged
    {
        if (!_attribute.Value.HasValue)
            throw new Exception("The attribute contains no data.");

        var value = _attribute.Value.Value;

        if (value.ValueKind != JsonValueKind.Array)
            throw new Exception($"Invalid value kind {value.ValueKind}.");

        var data = JsonSerializer.Deserialize<T[]>(value) 
            ?? throw new Exception($"Unable to deserialize data.");
            
        return data;
    }

    public T[] ReadCompound<T>(Func<FieldInfo, string?>? getName = null) where T : struct
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
    }

    public Dictionary<string, object?>[] ReadCompound()
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
    }

    public string[] ReadString()
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
    }

    public T[]?[] ReadVariableLength<T>(
        Selection? fileSelection = null, 
        Selection? memorySelection = null, 
        ulong[]? memoryDims = null) where T : struct
    {
        throw new NotImplementedException("This method is not (yet) implemented in the HSDS VOL connector.");
    }
}